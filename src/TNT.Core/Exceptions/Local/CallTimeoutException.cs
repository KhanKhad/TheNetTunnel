using System;

namespace TNT.Core.Exceptions.Local
{
    public class CallTimeoutException: Exception
    {
        public short MessageId { get; }
        public int AskId { get; }

        public CallTimeoutException(short messageId, int askId)
            : base("Answer timeout elapsed", null)
        {
            MessageId = messageId;
            AskId = askId;
        }

    }
}
