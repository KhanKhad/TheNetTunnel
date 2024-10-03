using System;

namespace TNT.Core.Exceptions.Local
{
    public abstract class TntCallException: Exception
    {
        protected TntCallException(
            bool isFatal, 
            short? messageId, 
            int? askId, 
            string message,
            Exception innerException  = null)
            :base(message, innerException)
        {
            IsFatal = isFatal;
            MessageId = messageId;
            AskId = askId;
        }

        public bool IsFatal { get; }
        public short? MessageId { get; }
        public int? AskId { get; }
    }
}
