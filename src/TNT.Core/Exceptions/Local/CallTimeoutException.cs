using System;

namespace TNT.Core.Exceptions.Local
{
    public class CallTimeoutException: Exception
    {
        public short MessageId { get; }
        public short AskId { get; }

        public CallTimeoutException(short messageId, short askId)
            : base("Answer timeout elapsed", null)
        {
            MessageId = messageId;
            AskId = askId;
        }

    }
}
