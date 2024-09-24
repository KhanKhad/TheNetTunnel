using System;

namespace TNT.Core.Presentation.ReceiveDispatching
{
    public class NotThreadDispatcher: IDispatcher
    {
        public void Set(RequestMessage message)
        {
            OnNewMessage?.Invoke(this, message);
        }

        public event Action<IDispatcher, RequestMessage> OnNewMessage;
        public void Release()
        {
            
        }
    }
}