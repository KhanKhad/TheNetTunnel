using System;
using System.IO;
using System.Linq;
using TNT.Core.Contract;
using TNT.Core.Exceptions.Remote;
using TNT.Core.Presentation.Deserializers;

namespace TNT.Core.Presentation
{
    public class MessagesDeserializer
    {
        private MethodsDescriptor _methodsDescriptor;

        public MessagesDeserializer(MethodsDescriptor methodsDescriptor)
        {
            _methodsDescriptor = methodsDescriptor;
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

            MethodDesctiption methodDescription = null;

            if((MessageType)messageType == MessageType.RequestMessage ||
                (MessageType)messageType == MessageType.SuccessfulResponseMessage)
            {
                if (!_methodsDescriptor.DescribedMethods.TryGetValue(messageId, out methodDescription))
                {
                    var rError = new ErrorMessage(messageId, askId,
                            ErrorType.ContractSignatureError,
                            $"Message with contract id {messageId} is not implemented");

                    return new MessageDeserializeResult()
                    {
                        ErrorMessageOrNull = rError,
                    };
                }
            }

            switch ((MessageType)messageType)
            {
                #region Ping
                case MessageType.PingMessage:
                case MessageType.PingResponseMessage:

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
                            MessageOrNull = new TntMessage()
                            {
                                MessageId = messageId,
                                MessageType = (MessageType)messageType,
                                AskId = askId,
                                Result = pingStatus,
                            },
                        };
                    }
                #endregion

                #region Request/Response
                case MessageType.RequestMessage:

                    try
                    {
                        object[] args;

                        if (methodDescription.HasArguments)
                        {
                            args = Deserialize(methodDescription.ArgumentsDeserializer,
                            methodDescription.ArgumentsCount, streamMessage);
                        }
                        else
                            args = Array.Empty<object>();

                        TntMessage tntMessage;

                        tntMessage = new TntMessage()
                        {
                            MessageId = messageId,
                            MessageType = (MessageType)messageType,
                            AskId = askId,
                            Result = args,
                        };

                        return new MessageDeserializeResult()
                        {
                            IsSuccessful = true,
                            MessageOrNull = tntMessage,
                        };

                    }
                    catch (Exception ex)
                    {
                        ErrorMessage dError;

                        dError = new ErrorMessage(messageId, askId,
                                 ErrorType.SerializationError, $"Message with contract id {messageId} cannot" +
                                 $" be deserialized. InnerException: {ex}");

                        return new MessageDeserializeResult()
                        {
                            ErrorMessageOrNull = dError,
                            NeedToDisconnect = true,
                        };
                    }

                case MessageType.SuccessfulResponseMessage:

                    try
                    {
                        object rObject;

                        if (methodDescription.HasReturnType)
                        {
                            rObject = Deserialize(methodDescription.ReturnTypeDeserializer, 1, streamMessage).Single();
                        }
                        else
                            rObject = null;

                        TntMessage tntMessage;

                        tntMessage = new TntMessage()
                        {
                            MessageId = messageId,
                            MessageType = (MessageType)messageType,
                            AskId = askId,
                            Result = rObject,
                        };

                        return new MessageDeserializeResult()
                        {
                            IsSuccessful = true,
                            MessageOrNull = tntMessage,
                        };

                    }
                    catch (Exception ex)
                    {
                        ErrorMessage dError;

                        dError = new ErrorMessage(messageId, askId,
                                 ErrorType.SerializationError, "Answer deserialization failed: " + ex.Message);

                        return new MessageDeserializeResult()
                        {
                            ErrorMessageOrNull = dError,
                            NeedToDisconnect = true,
                        };
                    }
                #endregion

                case MessageType.FailedResponseMessage:
                case MessageType.FatalFailedResponseMessage:

                    var errorDeserializer = new ErrorMessageDeserializer();
                    var deserializedError = errorDeserializer.Deserialize(streamMessage,
                        (int)(streamMessage.Length - streamMessage.Position));

                    return new MessageDeserializeResult()
                    {
                        IsSuccessful = true,
                        MessageOrNull = new TntMessage()
                        {
                            MessageId = messageId,
                            MessageType = (MessageType)messageType,
                            AskId = askId,
                            Result = deserializedError,
                        },
                    };

                case MessageType.Unknown:
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

        public object[] Deserialize(IDeserializer deserializer, int argumentsCount, MemoryStream data)
        {
            object[] arg = null;
            if (argumentsCount == 0)
                arg = Array.Empty<object>();
            else if (argumentsCount == 1)
                arg = new[] { deserializer.Deserialize(data, (int)(data.Length - data.Position)) };
            else
                arg = (object[])deserializer.Deserialize(data, (int)(data.Length - data.Position));
            return arg;
        }
    }


    public class MessageDeserializeResult
    {
        public TntMessage MessageOrNull { get; set; }
        public ErrorMessage ErrorMessageOrNull { get; set; }
        public bool NeedToDisconnect { get; set; }
        public bool IsSuccessful { get; set; }

        public MessageDeserializeResult()
        {

        }
    }
}
