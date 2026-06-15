using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using TheNetTunnel.Exceptions.Remote;
using TheNetTunnel.ReceiveDispatching;

namespace TheNetTunnel.Presentation
{
    public class Responser
    {
        private IDispatcher _receiveDispatcher;
        private MethodsDescriptor _methodsDescriptor;

        public Responser(MethodsDescriptor methodsDescriptor, IDispatcher receiveDispatcher)
        {
            _methodsDescriptor = methodsDescriptor;
            _receiveDispatcher = receiveDispatcher;
        }

        public async Task<TntMessage> CreateResponseAsync(TntMessage deserialized)
        {
            TntMessage result;

            var contractId = deserialized.ContractId;
            var id = deserialized.MessageId;
            var askId = deserialized.AskId;

            try
            {
                var arguments = (object[])deserialized.Result;

                if (_methodsDescriptor.DescribedMethods.TryGetValue(id, out var messageHandler))
                {
                    switch (messageHandler.MethodType)
                    {
                        case MethodTypes.SyncWithoutResult:
                            await _receiveDispatcher.HandleSyncSayMessage(messageHandler.MethodHandler, arguments).ConfigureAwait(false);
                            result = CreateSuccessfulResponseMessage(null, id, askId, contractId);
                            break;
                        case MethodTypes.SyncWithResult:
                            var sanswer = await _receiveDispatcher.HandleSyncAskMessage(messageHandler.MethodHandler, arguments).ConfigureAwait(false);
                            result = CreateSuccessfulResponseMessage(sanswer, id, askId, contractId);
                            break;
                        case MethodTypes.AsyncWithoutResult:
                            await _receiveDispatcher.HandleAsyncSayMessage(messageHandler.MethodHandler, arguments).ConfigureAwait(false);
                            result = CreateSuccessfulResponseMessage(null, id, askId, contractId);
                            break;
                        case MethodTypes.AsyncWithResult:
                            var aanswer = await _receiveDispatcher.HandleAsyncAskMessage(messageHandler.MethodHandler, arguments).ConfigureAwait(false);
                            result = CreateSuccessfulResponseMessage(aanswer, id, askId, contractId);
                            break;
                        default:

                            var error = new ErrorMessage(id, askId, ErrorType.ContractSignatureError,
                                $"UT|there isn't any handlers for this message: {id}|{askId}");

                            result = CreateFatalFailedResponseMessage(error, id, askId, contractId);

                            break;

                    }
                }
                else
                {
                    var error = new ErrorMessage(id, askId, ErrorType.ContractSignatureError,
                        $"there isn't any handlers for this message: {id}|{askId}");

                    result = CreateFatalFailedResponseMessage(error, id, askId, contractId);
                }
            }
            catch (Exception ex)
            {
                var error = new ErrorMessage(id, askId, ErrorType.UnhandledUserExceptionError,
                        $"Unexpected exception {id}|{askId}: {ex.Message}");

                result = CreateFailedResponseMessage(error, id, askId, contractId);
            }

            return result;
        }

        public static (bool needDisconnect, TntMessage message)  CreateHelloMessageResponse(InterlocutorProperties properties, TntMessage msgRequest)
        {
            var request = msgRequest.Result as HelloMessageRequest;

            var myVersion = properties.ServerMode ? properties.ServerVersion : properties.ClientVersion;
            var minimalVersion = properties.ServerMode ? properties.MinimalClientVersion : properties.MinimalServerVersion;

            HelloMessageResponse response;

            if (request.MyVersion < minimalVersion || request.MinimalVersion > myVersion)
                response = HelloMessageResponse.UnavailableVersion();
            else if (properties.ServerMode && properties.Fullmode)
                response = HelloMessageResponse.ConnectionsLimit();
            else
                response = HelloMessageResponse.Available();

            var msg = new TntMessage()
            {
                AskId = msgRequest.AskId,
                MessageId = msgRequest.MessageId,
                MessageType = MessageType.HelloMessageResponse,
                ContractId = msgRequest.ContractId,
                Result = response
            };

            return (!response.AvailableForWork, msg);
        }

        public static TntMessage CreateContractIdIsNotSuppertedHelloMessageResponse(TntMessage msgRequest)
        {
            var msg = new TntMessage()
            {
                AskId = msgRequest.AskId,
                MessageId = msgRequest.MessageId,
                MessageType = MessageType.HelloMessageResponse,
                ContractId = msgRequest.ContractId,
                Result = HelloMessageResponse.ContractIdIsNotSupported(),
            };

            return msg;
        }

        public static TntMessage CreatePingResponse(TntMessage msg, byte contractId)
        {
            return new TntMessage()
            {
                AskId = msg.AskId,
                MessageType = MessageType.PingResponseMessage,
                ContractId = contractId,
                Result = (short)1
            };
        }

        public static TntMessage CreateSuccessfulResponseMessage(object result, short messageId, int askId, byte contractId)
        {
            return new TntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = MessageType.SuccessfulResponseMessage,
                ContractId = contractId,
                Result = result,
            };
        }
        public static TntMessage CreateFailedResponseMessage(ErrorMessage errorMessage, short messageId, int askId, byte contractId)
        {
            return new TntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = MessageType.FailedResponseMessage,
                ContractId = contractId,
                Result = errorMessage,
            };
        }
        public static TntMessage CreateFatalFailedResponseMessage(ErrorMessage errorMessage, short messageId, int askId, byte contractId)
        {
            return new TntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = MessageType.FatalFailedResponseMessage,
                ContractId = contractId,
                Result = errorMessage,
            };
        }
    }
}
