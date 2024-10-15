using System;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TNT.Core.ReceiveDispatching
{
    public class ReceiveDispatcher : IDispatcher
    {
        private Channel<DispatcherTask> TasksChannel { get; }

        private int _maxAskId = 0;
        private bool _singleOperationMode;

        private ConcurrentDictionary<int, TaskCompletionSource<object>> MessageAwaiters;

        public ReceiveDispatcher(bool singleOperationMode = true)
        {
            TasksChannel = Channel.CreateUnbounded<DispatcherTask>();

            _singleOperationMode = singleOperationMode;

            MessageAwaiters = new ConcurrentDictionary<int, TaskCompletionSource<object>>();
        }

        private volatile bool _alreadyStarted;

        public void Start()
        {
            if (_alreadyStarted)
                return;

            _alreadyStarted = true;

            //we need to clear the SynchronisationContext
            _ = Task.Run(ReadChannelAsync);
        }

        private object _contract;
        public void SetContract<TContract>(TContract contract) where TContract : class
        {
            _contract = contract;
        }

        private async Task ReadChannelAsync()
        {
            var reader = TasksChannel.Reader;

            await foreach (var dTask in reader.ReadAllAsync())
            {
                var task = HandleDispatcherTask(dTask);

                if (_singleOperationMode)
                    await task;
            }
        }

        public async Task HandleDispatcherTask(DispatcherTask dTask)
        {
            await Task.Yield();

            try
            {
                object result = null;

                switch (dTask.DispatcherTaskType)
                {
                    case DispatcherTaskTypes.SyncSayMessage:
                        dTask.MethodInfo.Invoke(_contract, dTask.Args);
                        break;
                    case DispatcherTaskTypes.SyncAskMessage:
                        result = dTask.MethodInfo.Invoke(_contract, dTask.Args);
                        break;

                    case DispatcherTaskTypes.AsyncSayMessage:
                        var task = (Task)dTask.MethodInfo.Invoke(_contract, dTask.Args);

                        //If user doesnt subscribe on Funk<Task> here will be null
                        if (task != null)
                            await task;

                        break;
                    case DispatcherTaskTypes.AsyncAskMessage:
                        var taskWithResult = (Task)dTask.MethodInfo.Invoke(_contract, dTask.Args);

                        //If user doesnt subscribe on Funk<Task> here will be null
                        if (taskWithResult != null)
                        {
                            await taskWithResult.ConfigureAwait(false);

                            var resultProperty = taskWithResult.GetType().GetProperty("Result");
                            result = resultProperty.GetValue(taskWithResult);
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

            await TasksChannel.Writer.WriteAsync(dTask);

            await awaiter;
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

            await TasksChannel.Writer.WriteAsync(dTask);

            var result = await awaiter;

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

            await TasksChannel.Writer.WriteAsync(dTask);

            var result = await awaiter;
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

            await TasksChannel.Writer.WriteAsync(dTask);

            var result = await awaiter;

            return result;
        }

        public Task<object> GetAsyncMessageAwaiter(int askId)
        {
            var tks = new TaskCompletionSource<object>();

            if (MessageAwaiters.TryAdd(askId, tks))
                return tks.Task;

            else throw new Exception("Same askId was already added");
        }

        private volatile bool _disposed;
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

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