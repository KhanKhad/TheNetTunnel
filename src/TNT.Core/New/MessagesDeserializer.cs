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
    public static class MessagesDeserializer
    {
        public static MessageDeserializeResult Deserialize(MemoryStream message)
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

            _inputSayMessageDeserializeInfos.TryGetValue(id, out var sayDeserializer);

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
                    var error = new ErrorMessage(id,
                        askId, ErrorType.SerializationError, "Answer deserialization failed: " + ex.Message);

                    return new MessageDeserializeResult()
                    {
                        DeserializeResult = DeserializeResults.ExternalError,
                        ErrorMessageOrNull = error,
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
                //input answer message handling
                OnAns?.Invoke(this, id, askId.Value, deserialized.Single());

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
                //input ask / say messageHandling
                OnRequest?.Invoke(this, new RequestMessage(id, askId, deserialized));
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

    public enum DeserializeResults
    {
        InternalError,
        ExternalError,
        Request,
        Response
    }
}
