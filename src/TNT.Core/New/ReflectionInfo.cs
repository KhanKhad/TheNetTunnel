using System;
using System.Collections.Generic;
using System.Text;
using TNT.Core.Presentation.Deserializers;
using TNT.Core.Presentation.Serializers;
using TNT.Core.Presentation;
using TNT.Core.Transport;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Reflection;

namespace TNT.Core.New
{
    public class ReflectionInfo
    {

        /// <summary>
        /// input types serializer
        /// </summary>
        internal readonly Dictionary<int, InputMessageDeserializeInfo> _inputSayMessageDeserializeInfos
            = new Dictionary<int, InputMessageDeserializeInfo>();


        /// <summary>
        /// out types serializer
        /// </summary>
        internal readonly Dictionary<int, ISerializer> _outputSayMessageSerializes
            = new Dictionary<int, ISerializer>();


        /// <summary>
        /// Say handlers
        /// </summary>
        internal readonly ConcurrentDictionary<int, MethodInfo> _saySubscribtion
            = new ConcurrentDictionary<int, MethodInfo>();

        /// <summary>
        /// ask handlers
        /// </summary>
        internal readonly ConcurrentDictionary<int, MethodInfo> _askSubscribtion
           = new ConcurrentDictionary<int, MethodInfo>();

        /// <summary>
        /// Say handlers
        /// </summary>
        internal readonly ConcurrentDictionary<int, MethodInfo> _sayAsyncSubscribtion
            = new ConcurrentDictionary<int, MethodInfo>();

        /// <summary>
        /// ask handlers
        /// </summary>
        internal readonly ConcurrentDictionary<int, MethodInfo> _askAsyncSubscribtion
           = new ConcurrentDictionary<int, MethodInfo>();


        public ReflectionInfo(
            SerializerFactory serializerFactory,
            DeserializerFactory deserializerFactory,
            MessageTypeInfo[] outputMessages,
            MessageTypeInfo[] inputMessages)
        {
            foreach (var messageSayInfo in outputMessages)
            {
                var serializer = serializerFactory.Create(messageSayInfo.ArgumentTypes);
                _outputSayMessageSerializes.Add(messageSayInfo.MessageId, serializer);

                var returnType = messageSayInfo.ReturnType;

                if (returnType == typeof(void))
                {

                }
                else if(returnType == typeof(Task))
                {
                    _inputSayMessageDeserializeInfos.Add(
                            messageSayInfo.MessageId,
                            InputMessageDeserializeInfo.CreateForAnswer(
                                deserializerFactory.Create(messageSayInfo.ReturnType)));
                }
                else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var actualReturnType = returnType.GenericTypeArguments[0];

                    _inputSayMessageDeserializeInfos.Add(
                            messageSayInfo.MessageId,
                            InputMessageDeserializeInfo.CreateForAnswer(
                                deserializerFactory.Create(actualReturnType)));
                }
                else
                {
                    _inputSayMessageDeserializeInfos.Add(
                            messageSayInfo.MessageId,
                            InputMessageDeserializeInfo.CreateForAnswer(
                                deserializerFactory.Create(messageSayInfo.ReturnType)));
                }                
            }
            foreach (var messageSayInfo in inputMessages)
            {
                var hasReturnType = false;
                var deserializer = deserializerFactory.Create(messageSayInfo.ArgumentTypes);
                var returnType = messageSayInfo.ReturnType;

                if (returnType == typeof(void))
                {

                }
                else if (returnType == typeof(Task))
                {
                    _outputSayMessageSerializes.Add(messageSayInfo.MessageId,
                        serializerFactory.Create(returnType));
                }
                else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    hasReturnType = true;

                    var actualReturnType = returnType.GenericTypeArguments[0];

                    _outputSayMessageSerializes.Add(messageSayInfo.MessageId,
                        serializerFactory.Create(actualReturnType));
                }
                else
                {
                    hasReturnType = true;

                    _outputSayMessageSerializes.Add(messageSayInfo.MessageId,
                        serializerFactory.Create(messageSayInfo.ReturnType));
                }

                _inputSayMessageDeserializeInfos.Add(messageSayInfo.MessageId, InputMessageDeserializeInfo
                    .CreateForAsk(messageSayInfo.ArgumentTypes.Length, hasReturnType, deserializer));
            }
        }

        public void SetIncomeAskCallHandler(int messageId, MethodInfo callback)
        {
            _askSubscribtion.TryAdd(messageId, callback);
        }

        public void SetIncomeSayCallHandler(int messageId, MethodInfo callback)
        {
            _saySubscribtion.TryAdd(messageId, callback);
        }

        public void SetIncomeSayCallAsyncHandler(int messageId, MethodInfo callback)
        {
            _sayAsyncSubscribtion.TryAdd(messageId, callback);
        }

        public void SetIncomeAskCallAsyncHandler(int messageId, MethodInfo callback)
        {
            _askAsyncSubscribtion.TryAdd(messageId, callback);
        }

        public void Unsubscribe(int messageId)
        {
            _saySubscribtion.TryRemove(messageId, out _);
            _askSubscribtion.TryRemove(messageId, out _);
        }
    }
}
