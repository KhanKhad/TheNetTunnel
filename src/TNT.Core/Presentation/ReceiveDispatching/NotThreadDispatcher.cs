using System;
using System.Reflection;
using System.Threading.Tasks;

namespace TNT.Core.Presentation.ReceiveDispatching
{
    public class NotThreadDispatcher: IDispatcher
    {
        public Task Handle(Action<object[]> handler, object[] args)
        {
            handler.Invoke(args);
            return Task.CompletedTask;
        }

        public Task<object> Handle(Func<object[], object> handler, object[] args)
        {
            return Task.FromResult(handler.Invoke(args));
        }

        public void Dispose()
        {

        }

        public void Start()
        {

        }

        public Task HandleAsync(Func<object[], Task> handler, object[] args)
        {
            throw new NotImplementedException();
        }

        public Task<object> HandleAsync(Func<object[], Task<object>> handler, object[] args)
        {
            throw new NotImplementedException();
        }

        public void SetContract<TContract>(TContract contract) where TContract : class
        {
            throw new NotImplementedException();
        }

        public Task HandleSyncSayMessage(MethodInfo handler, object[] args)
        {
            throw new NotImplementedException();
        }

        public Task<object> HandleSyncAskMessage(MethodInfo handler, object[] args)
        {
            throw new NotImplementedException();
        }

        public Task HandleAsyncSayMessage(MethodInfo handler, object[] args)
        {
            throw new NotImplementedException();
        }

        public Task<object> HandleAsyncAskMessage(MethodInfo handler, object[] args)
        {
            throw new NotImplementedException();
        }

        public Task HandleEvent(Action<object[]> handler, object[] args)
        {
            throw new NotImplementedException();
        }

        public Task<object> HandleFunc(Func<object[], object> handler, object[] args)
        {
            throw new NotImplementedException();
        }
    }
}