using System;
using System.IO;
using System.Linq;
using TNT.Core.Exceptions.Remote;
using TNT.Core.Presentation;

namespace TNT.Core.New
{
    public class MessagesDeserializer
    {
        private NewReflectionHelper _reflectionHelper;

        public MessagesDeserializer(NewReflectionHelper reflectionHelper)
        {
            _reflectionHelper = reflectionHelper;
        }

        public MessageDeserializeResult Deserialize(MemoryStream streamMessage)
        {
            if (!streamMessage.TryReadShort(out var messageContractId))
            {
                var error = new ErrorMessage(null, null, ErrorType.SerializationError, "Message contract id is missed");

                return new MessageDeserializeResult()
                {
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = true,
                };
            }

            if (!streamMessage.TryReadShort(out var messageType))
            {
                var error = new ErrorMessage(
                            messageContractId, null,
                            ErrorType.SerializationError,
                            "MessageType is missed");

                return new MessageDeserializeResult()
                {
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = true,
                };
            }

            if (!streamMessage.TryReadShort(out var askId))
            {
                var error = new ErrorMessage(
                            messageContractId, null,
                            ErrorType.SerializationError,
                            "Ask Id is missed");

                return new MessageDeserializeResult()
                {
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = true,
                };
            }

            _reflectionHelper._inputSayMessageDeserializeInfos.TryGetValue(messageContractId, out var sayDeserializer);

            if (sayDeserializer == null)
            {
                var error = new ErrorMessage(messageContractId, streamMessage.TryReadShort(),
                        ErrorType.ContractSignatureError,
                        $"Message with contract id {messageContractId} is not implemented");
                
                return new MessageDeserializeResult()
                {
                    ErrorMessageOrNull = error,
                };
            }

            object[] deserialized;

            try
            {
                deserialized = sayDeserializer.Deserialize(streamMessage);
            }
            catch (Exception ex)
            {
                ErrorMessage error;

                if (messageContractId < 0)
                {
                    error = new ErrorMessage(messageContractId, askId,
                        ErrorType.SerializationError, "Answer deserialization failed: " + ex.Message);
                }
                else
                {
                    error = new ErrorMessage(messageContractId, askId,
                        ErrorType.SerializationError, $"Message with contract id {messageContractId} cannot be deserialized. InnerException: {ex}");
                }

                return new MessageDeserializeResult()
                {
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = true,
                };
            }

            if(deserialized.Length != 1)
            {
                var error = new ErrorMessage(messageContractId, streamMessage.TryReadShort(),
                        ErrorType.SerializationError,
                        $"Message with contract {messageContractId} is bad");

                return new MessageDeserializeResult()
                {
                    ErrorMessageOrNull = error,
                };
            }

            var tntMessage = new NewTntMessage()
            {
                MessageId = messageContractId,
                MessageType = (TntMessageType)messageType,
                AskId = askId,
                Result = deserialized.Single(),
            };

            var result = new MessageDeserializeResult()
            {
                IsSuccessful = true,
                MessageOrNull = tntMessage,
            };

            return result;
        }
    }

    public class MessageDeserializeResult
    {
        public NewTntMessage MessageOrNull {  get; set; }
        public ErrorMessage ErrorMessageOrNull { get; set; }
        public bool NeedToDisconnect {  get; set; }
        public bool IsSuccessful {  get; set; }

        public MessageDeserializeResult()
        {

        }
    }
}
