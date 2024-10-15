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

        public async Task<NewTntMessage> CreateResponseAsync(NewTntMessage deserialized)
        {
            NewTntMessage result;

            var type = deserialized.MessageType;

            var id = deserialized.MessageId;
            var askId = deserialized.AskId;

            try
            {
                var arguments = (object[])deserialized.Result;

                var messageHandler = _methodsDescriptor.DescribedMethods[id];

                if (_methodsDescriptor.DescribedMethods.TryGetValue(id, out var askHandler))
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

        public NewTntMessage CreatePingResponse(NewTntMessage msg)
        {
            return new NewTntMessage()
            {
                AskId = msg.AskId,
                MessageType = TntMessageType.PingResponseMessage,
                Result = (short)1
            };
        }

        public NewTntMessage CreateSuccessfulResponseMessage(object result, short messageId, int askId)
        {
            return new NewTntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = TntMessageType.SuccessfulResponseMessage,
                Result = result,
            };
        }
        public NewTntMessage CreateFailedResponseMessage(ErrorMessage errorMessage, short messageId, int askId)
        {
            return new NewTntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = TntMessageType.FailedResponseMessage,
                Result = errorMessage,
            };
        }
        public NewTntMessage CreateFatalFailedResponseMessage(ErrorMessage errorMessage, short messageId, int askId)
        {
            return new NewTntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = TntMessageType.FatalFailedResponseMessage,
                Result = errorMessage,
            };
        }
    }
}
