using System;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TNT.Core.Presentation.ReceiveDispatching
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
                    await task;
                    break;
                case DispatcherTaskTypes.AsyncAskMessage:
                    var taskWithResult = (Task)dTask.MethodInfo.Invoke(_contract, dTask.Args);

                    await taskWithResult.ConfigureAwait(false);

                    var resultProperty = taskWithResult.GetType().GetProperty("Result");
                    result = resultProperty.GetValue(taskWithResult);
                    break;



                case DispatcherTaskTypes.EventTask:
                    dTask.ActionHandler.Invoke(dTask.Args);
                    break;
                case DispatcherTaskTypes.FuncTask:
                    result = dTask.FunkHandler.Invoke(dTask.Args);
                    break;
            }

            if (MessageAwaiters.TryRemove(dTask.Id, out var taskAwaiter))
                taskAwaiter.SetResult(result);
        }

        public async Task Handle(Action<object[]> handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                ActionHandler = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.SyncSayMessage,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask);

            await awaiter;
        }

        public async Task<object> Handle(Func<object[], object> handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                FunkHandler = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.SyncAskMessage,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask);

            var result = await awaiter;

            return result;
        }

        public async Task HandleAsync(Func<object[], Task> handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                AsyncActionTask = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.AsyncSayMessage,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask);

            var result = await awaiter;
        }

        public async Task<object> HandleAsync(Func<object[], Task<object>> handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                AsyncFuncTask = handler,
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

        public async Task Handle(MethodInfo handler, object[] args)
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

        public async Task<object> HandleWithResult(MethodInfo handler, object[] args)
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

        public async Task HandleAsync(MethodInfo handler, object[] args)
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

        public async Task<object> HandleWithResultAsync(MethodInfo handler, object[] args)
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


        public async Task HandleEvent(Action<object[]> handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                ActionHandler = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.EventTask,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask);

            var result = await awaiter;
        }

        public async Task<object> HandleFunc(Func<object[], object> handler, object[] args)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var dTask = new DispatcherTask()
            {
                Id = newId,
                FunkHandler = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.FuncTask,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask);

            var result = await awaiter;

            return result;
        }
    }

    public class DispatcherTask
    {
        public DispatcherTask() { }

        public DispatcherTaskTypes DispatcherTaskType;

        public Func<object[], object> FunkHandler;
        public Action<object[]> ActionHandler;
        public Func<object[], Task> AsyncActionTask;
        public Func<object[], Task<object>> AsyncFuncTask;

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


        EventTask,
        FuncTask,
    }
}