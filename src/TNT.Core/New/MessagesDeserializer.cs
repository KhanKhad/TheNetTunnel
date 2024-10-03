using System;
using System.IO;
using System.Linq;
using TNT.Core.Contract;
using TNT.Core.Exceptions.Remote;
using TNT.Core.Presentation;
using TNT.Core.Presentation.Deserializers;

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
            if (!streamMessage.TryReadShort(out var messageId))
            {
                var error = new ErrorMessage(0, 0, ErrorType.SerializationError, "Message contract id is missed");

                return new MessageDeserializeResult()
                {
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = true,
                };
            }

            if (!streamMessage.TryReadShort(out var messageType))
            {
                var error = new ErrorMessage(
                            messageId, 0,
                            ErrorType.SerializationError,
                            "MessageType is missed");

                return new MessageDeserializeResult()
                {
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = true,
                };
            }

            if (!streamMessage.TryReadInt(out var askId))
            {
                var error = new ErrorMessage(
                            messageId, 0,
                            ErrorType.SerializationError,
                            "Ask Id is missed");

                return new MessageDeserializeResult()
                {
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = true,
                };
            }

            switch ((TntMessageType)messageType)
            {
                #region Ping
                case TntMessageType.PingMessage:
                case TntMessageType.PingResponseMessage:

                    if (!streamMessage.TryReadShort(out var pingStatus))
                    {
                        var perror = new ErrorMessage(
                                    messageId, 0,
                                    ErrorType.SerializationError,
                                    "No ping status in ping message");

                        return new MessageDeserializeResult()
                        {
                            ErrorMessageOrNull = perror,
                            NeedToDisconnect = true,
                        };
                    }
                    else //successfully read ping reply/response
                    {
                        return new MessageDeserializeResult()
                        {
                            IsSuccessful = true,
                            MessageOrNull = new NewTntMessage()
                            {
                                MessageId = messageId,
                                MessageType = (TntMessageType)messageType,
                                AskId = askId,
                                Result = pingStatus,
                            },
                        };
                    }
                #endregion

                #region Request/Response
                case TntMessageType.RequestMessage:
                case TntMessageType.SuccessfulResponseMessage:

                    if(!_reflectionHelper._inputSayMessageDeserializeInfos.TryGetValue(messageId, out var deserializer))
                    {
                        var rError = new ErrorMessage(messageId, askId,
                        ErrorType.ContractSignatureError,
                        $"Message with contract id {messageId} is not implemented");

                        return new MessageDeserializeResult()
                        {
                            ErrorMessageOrNull = rError,
                        };
                    }
                    else
                    {
                        try
                        {
                            var deserialized = deserializer.Deserialize(streamMessage);
                            
                            NewTntMessage tntMessage;

                            if (messageType != (short)TntMessageType.RequestMessage)
                            {
                                if (deserialized.Length != 1)
                                {
                                    return new MessageDeserializeResult()
                                    {
                                        ErrorMessageOrNull = new ErrorMessage(messageId, askId,
                                            ErrorType.SerializationError,
                                            $"Message with contract {messageId} is bad"),
                                    };
                                }

                                tntMessage = new NewTntMessage()
                                {
                                    MessageId = messageId,
                                    MessageType = (TntMessageType)messageType,
                                    AskId = askId,
                                    Result = deserialized.Single(),
                                };
                            }
                            else
                            {
                                tntMessage = new NewTntMessage()
                                {
                                    MessageId = messageId,
                                    MessageType = (TntMessageType)messageType,
                                    AskId = askId,
                                    Result = deserialized,
                                };
                            }

                            return new MessageDeserializeResult()
                            {
                                IsSuccessful = true,
                                MessageOrNull = tntMessage,
                            };

                        }
                        catch (Exception ex)
                        {
                            ErrorMessage dError;

                            if (messageType != (short)TntMessageType.RequestMessage)
                            {
                                dError = new ErrorMessage(messageId, askId,
                                    ErrorType.SerializationError, "Answer deserialization failed: " + ex.Message);
                            }
                            else
                            {
                                dError = new ErrorMessage(messageId, askId,
                                    ErrorType.SerializationError, $"Message with contract id {messageId} cannot" +
                                    $" be deserialized. InnerException: {ex}");
                            }

                            return new MessageDeserializeResult()
                            {
                                ErrorMessageOrNull = dError,
                                NeedToDisconnect = true,
                            };
                        }
                    }
                #endregion

                case TntMessageType.FailedResponseMessage:
                case TntMessageType.FatalFailedResponseMessage:

                    var errorDeserializer = new ErrorMessageDeserializer();
                    var deserializedError = errorDeserializer.Deserialize(streamMessage, 
                        (int)(streamMessage.Length - streamMessage.Position));

                    return new MessageDeserializeResult()
                    {
                        IsSuccessful = true,
                        MessageOrNull = new NewTntMessage()
                        {
                            MessageId = messageId,
                            MessageType = (TntMessageType)messageType,
                            AskId = askId,
                            Result = deserializedError,
                        },
                    };

                case TntMessageType.Unknown:
                default:

                    var error = new ErrorMessage(messageId, askId,
                        ErrorType.SerializationError,
                        $"Unknown message type: {messageType}");

                    return new MessageDeserializeResult()
                    {
                        ErrorMessageOrNull = error,
                        NeedToDisconnect = true
                    };
            }
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
