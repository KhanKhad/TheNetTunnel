using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TNT.Core.Exceptions.Local;
using TNT.Core.Presentation.Serializers;
using TNT.Core.Presentation;

namespace TNT.Core.New
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

        public MemoryStream SerializeTntMessage(NewTntMessage tntMessage)
        {
            var stream = new MemoryStream(1024);

            stream.Write(_reservedEmptyBuffer, 0, ReservedHeadLength);

            var messageId = tntMessage.MessageId;
            var messageType = tntMessage.MessageType;
            
            Tools.WriteShort(messageId, to: stream);
            Tools.WriteShort((short)messageType, to: stream);
            Tools.WriteInt(stream, tntMessage.AskId);

            var methodDescription = _methodsDescriptor.DescribedMethods[messageId];

            try
            {
                switch (messageType)
                {
                    case TntMessageType.PingMessage:
                    case TntMessageType.PingResponseMessage:

                        var pingVal = (short)tntMessage.Result;
                        Tools.WriteShort(pingVal, to: stream);

                        break;

                    case TntMessageType.RequestMessage:

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

                    case TntMessageType.SuccessfulResponseMessage:

                        if (methodDescription.HasReturnType)
                        {
                            var serializer = methodDescription.ReturnTypeSerializer;
                            serializer.Serialize(tntMessage.Result, stream);
                        }

                        break;

                    case TntMessageType.FailedResponseMessage:
                    case TntMessageType.FatalFailedResponseMessage:

                        var error = (ErrorMessage)tntMessage.Result;
                        new ErrorMessageSerializer().SerializeT(error, stream);

                        break;


                    case TntMessageType.Unknown:
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
