using System;
using System.Collections.Generic;
using System.Text;
using TNT.Core.Presentation.Deserializers;
using TNT.Core.Presentation.Serializers;
using TNT.Core.Presentation;
using TNT.Core.Transport;

namespace TNT.Core.New
{
    public class NewReflectionHelper
    {
        internal readonly Dictionary<int, InputMessageDeserializeInfo> _inputSayMessageDeserializeInfos
            = new Dictionary<int, InputMessageDeserializeInfo>();

        internal readonly Dictionary<int, ISerializer> _outputSayMessageSerializes
            = new Dictionary<int, ISerializer>();

        public NewReflectionHelper(
            SerializerFactory serializerFactory,
            DeserializerFactory deserializerFactory,
            MessageTypeInfo[] outputMessages,
            MessageTypeInfo[] inputMessages)
        {
            foreach (var messageSayInfo in outputMessages)
            {
                var serializer = serializerFactory.Create(messageSayInfo.ArgumentTypes);
                var hasReturnType = messageSayInfo.ReturnType != typeof(void);

                _outputSayMessageSerializes.Add(messageSayInfo.MessageId, serializer);
                if (hasReturnType)
                {
                    _inputSayMessageDeserializeInfos.Add(
                        -messageSayInfo.MessageId,
                        InputMessageDeserializeInfo.CreateForAnswer(
                            deserializerFactory.Create(messageSayInfo.ReturnType)));
                }
            }
            foreach (var messageSayInfo in inputMessages)
            {
                var hasReturnType = messageSayInfo.ReturnType != typeof(void);
                var deserializer = deserializerFactory.Create(messageSayInfo.ArgumentTypes);
                _inputSayMessageDeserializeInfos.Add(
                    messageSayInfo.MessageId,
                    InputMessageDeserializeInfo.CreateForAsk(messageSayInfo.ArgumentTypes.Length, hasReturnType,
                        deserializer));

                if (hasReturnType)
                {
                    _outputSayMessageSerializes.Add(-messageSayInfo.MessageId,
                        serializerFactory.Create(messageSayInfo.ReturnType));
                }
            }
            _inputSayMessageDeserializeInfos.Add(Messenger.ExceptionMessageTypeId,
                InputMessageDeserializeInfo.CreateForExceptionHandling());
        }
    }
}
