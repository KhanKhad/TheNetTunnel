﻿using System;
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
using TNT.Core.New.Tcp;
using TNT.Core.Presentation;
using TNT.Core.Presentation.ReceiveDispatching;
using TNT.Core.Transport;

namespace TNT.Core.New
{
    public class Responser
    {
        private ReflectionInfo _reflectionHelper;
        private IDispatcher _receiveDispatcher;


        public Responser(ReflectionInfo reflectionHelper, IDispatcher receiveDispatcher)
        {
            _reflectionHelper = reflectionHelper;
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

                if (_reflectionHelper._askSubscribtion.TryGetValue(id, out var askHandler))
                {
                    var answer = await _receiveDispatcher.HandleWithResult(askHandler, arguments);
                    result = CreateSuccessfulResponseMessage(answer, id, askId);
                }
                else if (_reflectionHelper._saySubscribtion.TryGetValue(id, out var sayHandler))
                {
                    await _receiveDispatcher.Handle(sayHandler, arguments);
                    result = CreateSuccessfulResponseMessage(null, id, askId);
                }
                else if (_reflectionHelper._sayAsyncSubscribtion.TryGetValue(id, out var sayAsyncHandler))
                {
                    await _receiveDispatcher.HandleAsync(sayAsyncHandler, arguments);
                    result = CreateSuccessfulResponseMessage(null, id, askId);
                }
                else if (_reflectionHelper._askAsyncSubscribtion.TryGetValue(id, out var askAsyncHandler))
                {
                    var answer = await _receiveDispatcher.HandleWithResultAsync(askAsyncHandler, arguments);
                    result = CreateSuccessfulResponseMessage(answer, id, askId);
                }
                else if (_reflectionHelper._eventSubscribtion.TryGetValue(id, out var eventHandler))
                {
                    await _receiveDispatcher.HandleEvent(eventHandler, arguments);
                    result = CreateSuccessfulResponseMessage(null, id, askId);
                }
                else if (_reflectionHelper._funcSubscribtion.TryGetValue(id, out var funcHandler))
                {
                    var answer = await _receiveDispatcher.HandleFunc(funcHandler, arguments);
                    result = CreateSuccessfulResponseMessage(answer, id, askId);
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
