using System;
using System.Collections.Generic;
using System.Text;

namespace TNT.Core.Presentation
{
    public class NewTntMessage
    {
        public NewTntMessage()
        {

        }

        public short MessageId;
        public TntMessageType MessageType;
        public int AskId;
        public object Result;
    }

    public enum TntMessageType : short
    {
        Unknown = 0,

        PingMessage = 1,
        PingResponseMessage = 2,

        RequestMessage = 3,

        SuccessfulResponseMessage = 4,
        FailedResponseMessage = 5,
        FatalFailedResponseMessage = 6,
    }
}
