using System;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TheNetTunnel.Diagnostics;

namespace TheNetTunnel.ReceiveDispatching
{
    public class ReceiveDispatcher : IDispatcher
    {
        private Channel<DispatcherTask> TasksChannel { get; }

        private int _maxAskId = 0;
        private bool _singleOperationMode;

        private ConcurrentDictionary<int, TaskCompletionSource<object>> MessageAwaiters;

        public ReceiveDispatcher(bool singleOperationMode = true)
        {
            TasksChannel = Channel.CreateUnbounded<DispatcherTask>(new UnboundedChannelOptions()
            {
                SingleReader = true,
            });

            _singleOperationMode = singleOperationMode;

            MessageAwaiters = new ConcurrentDictionary<int, TaskCompletionSource<object>>();
        }

        private CancellationTokenSource _workCts;
        private Task _readChannelAsync;

        public void Start()
        {
            _workCts = new CancellationTokenSource();
            _readChannelAsync = Task.Run(async () => await ReadChannelAsync(_workCts.Token));
        }

        private object _contract;
        public void SetContract<TContract>(TContract contract) where TContract : class
        {
            _contract = contract;
        }

        private async Task ReadChannelAsync(CancellationToken token)
        {
            var reader = TasksChannel.Reader;

            try
            {
                await foreach (var dTask in reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    var task = HandleDispatcherTask(dTask);

                    if (_singleOperationMode)
                        await task.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the dispatcher is being stopped.
            }
            catch (Exception e)
            {
                TntLog.Error(nameof(ReceiveDispatcher), "Dispatcher read loop terminated unexpectedly", e);
            }
        }

        public async Task HandleDispatcherTask(DispatcherTask dTask)
        {
            // In multi-operation mode the read loop fires tasks without awaiting them,
            // so we must yield to let several handlers run concurrently. In single-
            // operation mode the loop awaits each task, so running the handler inline
            // on the dispatcher thread is both correct and one thread-hop cheaper.
            if (!_singleOperationMode)
                await Task.Yield();

            try
            {
                object result = null;

                switch (dTask.DispatcherTaskType)
                {
                    case DispatcherTaskTypes.SyncSayMessage:
                        DelegateCache.Invoke(dTask.MethodInfo, _contract, dTask.Args);
                        break;
                    case DispatcherTaskTypes.SyncAskMessage:
                        result = DelegateCache.Invoke(dTask.MethodInfo, _contract, dTask.Args);
                        break;

                    case DispatcherTaskTypes.AsyncSayMessage:
                        var task = (Task)DelegateCache.Invoke(dTask.MethodInfo, _contract, dTask.Args);

                        //If user doesnt subscribe on Funk<Task> here will be null
                        if (task != null)
                            await task.ConfigureAwait(false);

                        break;
                    case DispatcherTaskTypes.AsyncAskMessage:
                        var taskWithResult = (Task)DelegateCache.Invoke(dTask.MethodInfo, _contract, dTask.Args);

                        //If user doesnt subscribe on Funk<Task> here will be null
                        if (taskWithResult != null)
                        {
                            await taskWithResult.ConfigureAwait(false);

                            result = DelegateCache.ReadTaskResult(taskWithResult);
                        }
                        else //we'll create a default value or null
                        {
                            var actualReturnType = dTask.MethodInfo.ReturnType.GenericTypeArguments[0];

                            if (actualReturnType.IsValueType)
                                result = Activator.CreateInstance(actualReturnType);
                        }

                        break;
                }

                if (MessageAwaiters.TryRemove(dTask.Id, out var taskAwaiter))
                    taskAwaiter.SetResult(result);
            }
            catch(Exception ex)
            {
                if (MessageAwaiters.TryRemove(dTask.Id, out var taskAwaiter))
                    taskAwaiter.SetException(ex);
            }
            
        }

        public async Task HandleSyncSayMessage(MethodInfo handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                MethodInfo = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.SyncSayMessage,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask).ConfigureAwait(false);

            await awaiter.ConfigureAwait(false);
        }

        public async Task<object> HandleSyncAskMessage(MethodInfo handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                MethodInfo = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.SyncAskMessage,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask).ConfigureAwait(false);

            var result = await awaiter.ConfigureAwait(false);

            return result;
        }

        public async Task HandleAsyncSayMessage(MethodInfo handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                MethodInfo = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.AsyncSayMessage,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask).ConfigureAwait(false);

            var result = await awaiter.ConfigureAwait(false);
        }

        public async Task<object> HandleAsyncAskMessage(MethodInfo handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                MethodInfo = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.AsyncAskMessage,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask).ConfigureAwait(false);

            var result = await awaiter.ConfigureAwait(false);

            return result;
        }

        public Task<object> GetAsyncMessageAwaiter(int askId)
        {
            var tks = new TaskCompletionSource<object>();

            if (MessageAwaiters.TryAdd(askId, tks))
                return tks.Task;

            else throw new Exception("Same askId was already added");
        }

        public void Dispose()
        {
            if (_workCts == null)
                return;

            _workCts.Cancel();

            _readChannelAsync.ConfigureAwait(false).GetAwaiter().GetResult();

            _workCts.Dispose();
            _workCts = null;

            TasksChannel.Writer.Complete();
        }

        public async ValueTask DisposeAsync()
        {
            if (_workCts == null)
                return;

            _workCts.Cancel();

            await _readChannelAsync.ConfigureAwait(false);

            _workCts.Dispose();
            _workCts = null;

            TasksChannel.Writer.Complete();
        }
    }

    public class DispatcherTask
    {
        public DispatcherTask() { }

        public DispatcherTaskTypes DispatcherTaskType;

        public MethodInfo MethodInfo;

        public object[] Args;

        public int Id;
    }
    public enum DispatcherTaskTypes
    {
        SyncSayMessage,
        SyncAskMessage,
        AsyncSayMessage,
        AsyncAskMessage,
    }
}