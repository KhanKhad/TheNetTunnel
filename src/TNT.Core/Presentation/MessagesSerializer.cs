using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TNT.Core.Exceptions.Local;
using TNT.Core.Exceptions.Remote;
using TNT.Core.Presentation.Serializers;

namespace TNT.Core.Presentation
{
    public class MessagesSerializer
    {
        private MethodsDescriptor _methodsDescriptor;

        public int ReservedHeadLength => sizeof(uint);
        private static readonly byte[] _reservedEmptyBuffer = new byte[sizeof(uint)];

        public MessagesSerializer(MethodsDescriptor methodsDescriptor)
        {
            _methodsDescriptor = methodsDescriptor;
        }

        public MemoryStream SerializeTntMessage(TntMessage tntMessage)
        {
            var stream = new MemoryStream(1024);
            stream.Write(_reservedEmptyBuffer, 0, ReservedHeadLength);

            var messageId = tntMessage.MessageId;
            var messageType = tntMessage.MessageType;

            Tools.WriteShort(messageId, to: stream);
            Tools.WriteShort((short)messageType, to: stream);
            stream.WriteInt(tntMessage.AskId);

            MethodDesctiption methodDescription = null;

            if (messageType == MessageType.RequestMessage ||
                messageType == MessageType.SuccessfulResponseMessage)
            {
                if (!_methodsDescriptor.DescribedMethods.TryGetValue(messageId, out methodDescription))
                {
                    var rError = new ErrorMessage(messageId, tntMessage.AskId,
                        ErrorType.ContractSignatureError,
                        $"Message with contract id {messageId} is not implemented");

                    var error = (ErrorMessage)tntMessage.Result;
                    new ErrorMessageSerializer().SerializeT(error, stream);

                    return stream;
                }
            }

            try
            {
                switch (messageType)
                {
                    case MessageType.PingMessage:
                    case MessageType.PingResponseMessage:

                        var pingVal = (short)tntMessage.Result;
                        Tools.WriteShort(pingVal, to: stream);

                        break;

                    case MessageType.RequestMessage:

                        if (methodDescription.HasArguments)
                        {
                            var serializer = methodDescription.ArgumentsSerializer;

                            var values = (object[])tntMessage.Result;

                            if (values.Length == 1)
                                serializer.Serialize(values[0], stream);
                            else if (values.Length > 1)
                                serializer.Serialize(values, stream);
                        }

                        break;

                    case MessageType.SuccessfulResponseMessage:

                        if (methodDescription.HasReturnType)
                        {
                            var serializer = methodDescription.ReturnTypeSerializer;
                            serializer.Serialize(tntMessage.Result, stream);
                        }

                        break;

                    case MessageType.FailedResponseMessage:
                    case MessageType.FatalFailedResponseMessage:

                        var error = (ErrorMessage)tntMessage.Result;
                        new ErrorMessageSerializer().SerializeT(error, stream);

                        break;


                    case MessageType.Unknown:
                    default:
                        throw new Exception("Unknown message type");
                }
            }
            catch (Exception ex)
            {
                //??
                throw ex;
            }

            stream.Position = 0;
            uint len = (uint)(stream.Length - ReservedHeadLength);
            stream.WriteUint(len);
            stream.Position = 0;

            return stream;
        }
    }
}
