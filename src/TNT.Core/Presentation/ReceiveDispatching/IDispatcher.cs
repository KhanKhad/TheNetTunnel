using System;
using System.Reflection;
using System.Threading.Tasks;

namespace TNT.Core.Presentation.ReceiveDispatching
{
    public interface IDispatcher : IDisposable
    {
        void Start();

        void SetContract<TContract>(TContract contract) where TContract : class;


        Task Handle(MethodInfo handler, object[] args);
        Task<object> HandleWithResult(MethodInfo handler, object[] args);
        Task HandleAsync(MethodInfo handler, object[] args);
        Task<object> HandleWithResultAsync(MethodInfo handler, object[] args);
    }
}