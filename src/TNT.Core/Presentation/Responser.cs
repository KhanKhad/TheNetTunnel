using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Local;
using TNT.Core.Exceptions.Remote;
using TNT.Core.ReceiveDispatching;
using TNT.Core.Transport;

namespace TNT.Core.Presentation
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
                            await _receiveDispatcher.HandleSyncSayMessage(messageHandler.MethodHandler, arguments);
                            result = CreateSuccessfulResponseMessage(null, id, askId);
                            break;
                        case MethodTypes.SyncWithResult:
                            var sanswer = await _receiveDispatcher.HandleSyncAskMessage(messageHandler.MethodHandler, arguments);
                            result = CreateSuccessfulResponseMessage(sanswer, id, askId);
                            break;
                        case MethodTypes.AsyncWithoutResult:
                            await _receiveDispatcher.HandleAsyncSayMessage(messageHandler.MethodHandler, arguments);
                            result = CreateSuccessfulResponseMessage(null, id, askId);
                            break;
                        case MethodTypes.AsyncWithResult:
                            var aanswer = await _receiveDispatcher.HandleAsyncAskMessage(messageHandler.MethodHandler, arguments);
                            result = CreateSuccessfulResponseMessage(aanswer, id, askId);
                            break;
                        default:

                            var error = new ErrorMessage(id, askId, ErrorType.ContractSignatureError,
                                $"UT|there isn't any handlers for this message: {id}|{askId}");

                            result = CreateFatalFailedResponseMessage(error, id, askId);

                            break;

                    }
                }
                else
                {
                    var error = new ErrorMessage(id, askId, ErrorType.ContractSignatureError,
                        $"there isn't any handlers for this message: {id}|{askId}");

                    result = CreateFatalFailedResponseMessage(error, id, askId);
                }
            }
            catch (Exception ex)
            {
                var error = new ErrorMessage(id, askId, ErrorType.UnhandledUserExceptionError,
                        $"Unexpected exception {id}|{askId}: {ex.Message}");

                result = CreateFailedResponseMessage(error, id, askId);
            }

            return result;
        }

        public (bool needDisconnect, TntMessage message)  CreateHelloMessageResponse(InterlocutorProperties properties, TntMessage msgRequest)
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
                MessageType = MessageType.HelloMessageResponse,
                Result = response
            };

            return (!response.AvailableForWork, msg);
        }


        public TntMessage CreatePingResponse(TntMessage msg)
        {
            return new TntMessage()
            {
                AskId = msg.AskId,
                MessageType = MessageType.PingResponseMessage,
                Result = (short)1
            };
        }

        public TntMessage CreateSuccessfulResponseMessage(object result, short messageId, int askId)
        {
            return new TntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = MessageType.SuccessfulResponseMessage,
                Result = result,
            };
        }
        public TntMessage CreateFailedResponseMessage(ErrorMessage errorMessage, short messageId, int askId)
        {
            return new TntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = MessageType.FailedResponseMessage,
                Result = errorMessage,
            };
        }
        public TntMessage CreateFatalFailedResponseMessage(ErrorMessage errorMessage, short messageId, int askId)
        {
            return new TntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = MessageType.FatalFailedResponseMessage,
                Result = errorMessage,
            };
        }
    }
}
