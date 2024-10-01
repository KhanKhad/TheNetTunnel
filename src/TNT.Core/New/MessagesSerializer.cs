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
        private NewReflectionHelper _reflectionHelper;

        public int ReservedHeadLength => sizeof(uint);
        private static readonly byte[] _reservedEmptyBuffer = new byte[sizeof(uint)];

        public MessagesSerializer(NewReflectionHelper reflectionHelper)
        {
            _reflectionHelper = reflectionHelper;
        }

        public MemoryStream SerializeTntMessage(NewTntMessage tntMessage)
        {
            var stream = new MemoryStream(1024);

            stream.Write(_reservedEmptyBuffer, 0, ReservedHeadLength);

            var messageId = tntMessage.MessageId;
            var messageType = tntMessage.MessageType;
            
            Tools.WriteShort(messageId, to: stream);
            Tools.WriteShort((short)messageType, to: stream);
            Tools.WriteShort(tntMessage.AskId, to: stream);

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

                        var rqserializer = _reflectionHelper._outputSayMessageSerializes[messageId];

                        var values = (object[])tntMessage.Result;

                        if (values.Length == 1)
                            rqserializer.Serialize(values[0], stream);
                        else if (values.Length > 1)
                            rqserializer.Serialize(values, stream);

                        break;

                    case TntMessageType.SuccessfulResponseMessage:

                        var rsserializer = _reflectionHelper._outputSayMessageSerializes[messageId];
                        rsserializer.Serialize(tntMessage.Result, stream);

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

            return stream;
        }


        public MemoryStream SerializeSayMessage(short id, object[] values)
        {
            var stream = new MemoryStream(1024);

            stream.Write(_reservedEmptyBuffer, 0, ReservedHeadLength);

            _reflectionHelper._outputSayMessageSerializes.TryGetValue(id, out var serializer);

            Tools.WriteShort(id, to: stream);

            try
            {
                if (values.Length == 1)
                    serializer.Serialize(values[0], stream);
                else if (values.Length > 1)
                    serializer.Serialize(values, stream);
            }
            catch (Exception e)
            {
                throw new LocalSerializationException(null, null, "Serialization failed", e);
            }

            stream.Position = 0;

            return stream;
        }

        public MemoryStream SerializeAskMessage(short id, short askId, object[] values)
        {
            var stream = new MemoryStream(1024);

            stream.Write(_reservedEmptyBuffer, 0, ReservedHeadLength);

            _reflectionHelper._outputSayMessageSerializes.TryGetValue(id, out var serializer);

            Tools.WriteShort(id, to: stream);
            Tools.WriteShort(askId, to: stream);

            try
            {
                if (values.Length == 1)
                    serializer.Serialize(values[0], stream);
                else if (values.Length > 1)
                    serializer.Serialize(values, stream);
            }
            catch (Exception e)
            {
                throw new LocalSerializationException(null, null, "Serialization failed", e);
            }

            stream.Position = 0;

            return stream;
        }

        public MemoryStream SerializeErrorMessage(ErrorMessage errorInfo)
        {
            var stream = new MemoryStream(1024);

            stream.Write(_reservedEmptyBuffer, 0, ReservedHeadLength);

            Tools.WriteShort(Messenger.ExceptionMessageTypeId, to: stream);
            new ErrorMessageSerializer().SerializeT(errorInfo, stream);
            stream.Position = 0;

            return stream;
        }
    }
}
