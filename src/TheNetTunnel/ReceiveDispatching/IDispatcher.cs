using System;
using System.Reflection;
using System.Threading.Tasks;

namespace TheNetTunnel.ReceiveDispatching
{
    public interface IDispatcher : IDisposable, IAsyncDisposable
    {
        void Start();

        void SetContract<TContract>(TContract contract) where TContract : class;

        Task HandleSyncSayMessage(MethodInfo handler, object[] args);
        Task<object> HandleSyncAskMessage(MethodInfo handler, object[] args);
        Task HandleAsyncSayMessage(MethodInfo handler, object[] args);
        Task<object> HandleAsyncAskMessage(MethodInfo handler, object[] args);
    }
}