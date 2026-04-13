using System;
using System.Collections.Generic;
using System.Text;

namespace TNT.Core.Presentation
{
    public class TntMessage
    {
        public TntMessage()
        {

        }

        public short MessageId;
        public MessageType MessageType;
        public int AskId;
        public object Result;
    }

    public enum MessageType : short
    {
        Unknown = 0,

        Initialize = 1,
        InitializeResponse = 2,

        PingMessage = 3,
        PingResponseMessage = 4,

        RequestMessage = 5,

        SuccessfulResponseMessage = 6,
        FailedResponseMessage = 7,
        FatalFailedResponseMessage = 8,
    }
}
