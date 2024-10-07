using System;
using System.Threading.Tasks;

namespace TNT.Core.Presentation.ReceiveDispatching
{
    public interface IDispatcher : IDisposable
    {
        void Start();
        Task Handle(Action<object[]> handler, object[] args);
        Task<object> Handle(Func<object[], object> handler, object[] args);

        Task HandleAsync(Func<object[], Task> handler, object[] args);
        Task<object> HandleAsync(Func<object[], Task<object>> handler, object[] args);
    }
}