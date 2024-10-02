using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TNT.Core.Presentation.ReceiveDispatching
{
    public class ReceiveDispatcher : IDispatcher
    {
        private Channel<DispatcherTask> TasksChannel { get; }

        private volatile short _maxAskId = 0;
        private bool _singleOperationMode;

        private ConcurrentDictionary<short, TaskCompletionSource<object>> MessageAwaiters;
        
        public ReceiveDispatcher(bool singleOperationMode = true)
        {
            TasksChannel = Channel.CreateBounded<DispatcherTask>(5);

            _singleOperationMode = singleOperationMode;

            MessageAwaiters = new ConcurrentDictionary<short, TaskCompletionSource<object>>();
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

            if (dTask.DispatcherTaskType == DispatcherTaskTypes.FuncTask)
                result = dTask.FunkHandler.Invoke(dTask.Args);
            else
                dTask.ActionHandler.Invoke(dTask.Args);

            if (MessageAwaiters.TryRemove(dTask.Id, out var taskAwaiter))
                taskAwaiter.SetResult(result);
        }

        public async Task Handle(Action<object[]> handler, object[] args)
        {
            short newId;

            unchecked
            {
                newId = _maxAskId++;
            }

            var dTask = new DispatcherTask()
            {
                Id = newId,
                ActionHandler = handler,
                Args = args,
                DispatcherTaskType = DispatcherTaskTypes.ActionTask,
            };

            var awaiter = GetAsyncMessageAwaiter(newId);

            await TasksChannel.Writer.WriteAsync(dTask);

            await awaiter;
        }

        public async Task<object> Handle(Func<object[], object> handler, object[] args)
        {
            short newId;

            unchecked
            {
                newId = _maxAskId++;
            }

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


        public Task<object> GetAsyncMessageAwaiter(short askId)
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

        public Func<object[], object> FunkHandler;
        public Action<object[]> ActionHandler;

        public object[] Args;

        public short Id;
    }
    public enum DispatcherTaskTypes
    {
        ActionTask,
        FuncTask
    }
}