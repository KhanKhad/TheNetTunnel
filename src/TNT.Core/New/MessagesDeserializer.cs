using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using TNT.Core.Exceptions.Local;
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

        public MessageDeserializeResult Deserialize(MemoryStream message)
        {
            if (!message.TryReadShort(out var id))
            {
                var error = new ErrorMessage(null, null, ErrorType.SerializationError, "Messae type id missed");

                return new MessageDeserializeResult()
                {
                    DeserializeResult = DeserializeResults.InternalError,
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = true,
                };
            }

            _reflectionHelper._inputSayMessageDeserializeInfos.TryGetValue(id, out var sayDeserializer);

            if (sayDeserializer == null)
            {
                var error = new ErrorMessage(id, message.TryReadShort(),
                        ErrorType.ContractSignatureError,
                        $"Message type id {id} is not implemented");
                
                return new MessageDeserializeResult()
                {
                    DeserializeResult = DeserializeResults.InternalError,
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = false,
                };
            }

            short? askId = null;
            if (id < 0 || sayDeserializer.HasReturnType)
            {
                askId = message.TryReadShort();

                if (!askId.HasValue)
                {
                    var error = new ErrorMessage(
                            id, null,
                            ErrorType.SerializationError,
                            "Ask Id missed");

                    return new MessageDeserializeResult()
                    {
                        DeserializeResult = DeserializeResults.InternalError,
                        ErrorMessageOrNull = error,
                        NeedToDisconnect = true,
                    };
                }
            }
            object[] deserialized;

            try
            {
                deserialized = sayDeserializer.Deserialize(message);
            }
            catch (Exception ex)
            {
                if (id < 0)
                {
                    var errorEx = new ErrorMessage(id,
                        askId, ErrorType.SerializationError, "Answer deserialization failed: " + ex.Message);

                    return new MessageDeserializeResult()
                    {
                        DeserializeResult = DeserializeResults.ExternalError,
                        ErrorMessageOrNull = errorEx,
                        NeedToDisconnect = true,
                    };
                }

                var error = new ErrorMessage(
                        id, askId,
                        ErrorType.SerializationError,
                        $"Message type id{id} with could not be deserialized. InnerException: {ex}");

                return new MessageDeserializeResult()
                {
                    DeserializeResult = DeserializeResults.ExternalError,
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = true,
                };
            }

            if (id < 0)
            {
                var ansMsg = new ResponseMessage()
                {
                    Id = id,
                    AskId = askId.Value,
                    Answer = deserialized.Single()
                };

                //input answer message handling
                return new MessageDeserializeResult()
                {
                    DeserializeResult = DeserializeResults.Response,
                    MessageOrNull = ansMsg,
                };
            }
            else if (id == Messenger.ExceptionMessageTypeId)
            {
                var error = (ErrorMessage)deserialized.First();

                return new MessageDeserializeResult()
                {
                    DeserializeResult = DeserializeResults.ExternalError,
                    ErrorMessageOrNull = error,
                    NeedToDisconnect = error.Exception.IsFatal,
                };
            }
            else
            {
                var requestMsg = new RequestMessage(id, askId, deserialized);

                //input ask / say messageHandling
                return new MessageDeserializeResult()
                {
                    DeserializeResult = DeserializeResults.Request,
                    MessageOrNull = requestMsg,
                };
            }
        }
    }

    public class MessageDeserializeResult
    {
        public object MessageOrNull {  get; set; }
        public ErrorMessage ErrorMessageOrNull { get; set; }
        public bool NeedToDisconnect {  get; set; }
        public DeserializeResults DeserializeResult {  get; set; }

        public MessageDeserializeResult()
        {

        }
    }

    public class ResponseMessage
    {
        public short Id { get; set; }
        public short AskId { get; set; }
        public object Answer { get; set; }
    }

    public enum DeserializeResults
    {
        InternalError,
        ExternalError,
        Request,
        Response
    }
}
