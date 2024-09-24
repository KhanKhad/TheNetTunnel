using System;

namespace TNT.Core.Presentation.ReceiveDispatching
{
    public interface IDispatcher
    {
        void Set(RequestMessage message);
        event Action<IDispatcher, RequestMessage> OnNewMessage;
        void Release();
    }
}