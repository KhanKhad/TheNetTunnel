using System;
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
    }
}