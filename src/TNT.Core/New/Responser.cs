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
using TNT.Core.New.Tcp;
using TNT.Core.Presentation;
using TNT.Core.Presentation.ReceiveDispatching;
using TNT.Core.Transport;

namespace TNT.Core.New
{
    public class Responser
    {
        private NewReflectionHelper _reflectionHelper;


        public Responser(NewReflectionHelper reflectionHelper, IDispatcher receiveDispatcher)
        {
            _reflectionHelper = reflectionHelper;
        }

        public NewTntMessage CreateResponse(NewTntMessage deserialized)
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
                    var answer = askHandler.Invoke(arguments);

                    result = CreateSuccessfulResponseMessage(answer, id, (short)-askId);
                }
                else if (_reflectionHelper._saySubscribtion.TryGetValue(id, out var sayHandler))
                {
                    sayHandler.Invoke(arguments);

                    result = CreateSuccessfulResponseMessage(null, id, (short)-askId);
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

        public NewTntMessage CreateSuccessfulResponseMessage(object result, short messageId, short askId)
        {
            return new NewTntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = TntMessageType.SuccessfulResponseMessage,
                Result = result,
            };
        }
        public NewTntMessage CreateFailedResponseMessage(ErrorMessage errorMessage, short messageId, short askId)
        {
            return new NewTntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = TntMessageType.FailedResponseMessage,
                Result = errorMessage,
            };
        }
        public NewTntMessage CreateFatalFailedResponseMessage(ErrorMessage errorMessage, short messageId, short askId)
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
