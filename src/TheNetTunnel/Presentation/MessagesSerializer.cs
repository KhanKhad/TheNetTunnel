using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Exceptions.Remote;
using TheNetTunnel.Presentation.Serializers;

namespace TheNetTunnel.Presentation
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

        public PooledMemoryStream SerializeTntMessage(TntMessage tntMessage)
        {
            var stream = new PooledMemoryStream(1024)
            {
                AskId = tntMessage.AskId
            };

            try
            {
                stream.Write(_reservedEmptyBuffer, 0, ReservedHeadLength);

                var messageId = tntMessage.MessageId;
                var messageType = tntMessage.MessageType;

                Tools.WriteShort(messageId, to: stream);
                Tools.WriteShort((short)messageType, to: stream);
                stream.WriteInt(tntMessage.AskId);

                MethodDesctiption methodDescription = null;

                if (messageType is not MessageType.RequestMessage and not MessageType.SuccessfulResponseMessage
                    || _methodsDescriptor.DescribedMethods.TryGetValue(messageId, out methodDescription))
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

                        case MessageType.HelloMessageRequest:

                            new HelloMessageRequestSerializer().SerializeT((HelloMessageRequest)tntMessage.Result, stream);

                            break;

                        case MessageType.HelloMessageResponse:

                            new HelloMessageResponseSerializer().SerializeT((HelloMessageResponse)tntMessage.Result, stream);

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
                else
                {
                    var rError = new ErrorMessage(messageId, tntMessage.AskId, ErrorType.ContractSignatureError, $"Message with contract id {messageId} is not implemented");
                    new ErrorMessageSerializer().SerializeT(rError, stream);
                }

                stream.Position = 0;
                uint len = (uint)(stream.Length - ReservedHeadLength);
                stream.WriteUint(len);
                stream.Position = 0;

                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
    }
}
