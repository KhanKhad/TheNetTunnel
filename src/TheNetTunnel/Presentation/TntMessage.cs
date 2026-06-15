using System;
using System.Collections.Generic;
using System.Text;

namespace TheNetTunnel.Presentation
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

        /// <summary>
        /// Identifies which contract the message belongs to. Lets a single port
        /// expose several different contracts in future versions of the library.
        /// 255 is the default ("unspecified"/legacy single-contract) value.
        /// </summary>
        public byte ContractId;
    }

    public enum MessageType : short
    {
        Unknown = 0,

        HelloMessageRequest = 1,
        HelloMessageResponse = 2,

        PingMessage = 3,
        PingResponseMessage = 4,

        RequestMessage = 5,

        SuccessfulResponseMessage = 6,
        FailedResponseMessage = 7,
        FatalFailedResponseMessage = 8,

        DisconnectMessage = 255,
    }
}
