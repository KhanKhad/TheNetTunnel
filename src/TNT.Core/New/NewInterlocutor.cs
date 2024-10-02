﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Local;
using TNT.Core.New.Tcp;
using TNT.Core.Presentation;
using TNT.Core.Presentation.ReceiveDispatching;
using TNT.Core.Transport;

namespace TNT.Core.New
{
    public class NewInterlocutor : IInterlocutor
    {
        public IChannel Channel;
        public Responser _responser;

        private NewReflectionHelper _reflectionHelper;
        private MessagesSerializer _messagesSerializer;
        private MessagesDeserializer _messagesDeserializer;
        private readonly ReceivePduQueue _receiveMessageAssembler;

        private volatile short _maxAskId = 0;
        private readonly int _maxAnsDelay;

        private ConcurrentDictionary<short, TaskCompletionSource<object>> MessageAwaiters;

        public NewInterlocutor(NewReflectionHelper reflectionHelper, IDispatcher receiveDispatcher, 
            IChannel channel, int maxAnsDelay = 3000)
        {
            _maxAnsDelay = maxAnsDelay;

            Channel = channel;

            _reflectionHelper = reflectionHelper;
            _receiveMessageAssembler = new ReceivePduQueue();

            _messagesSerializer = new MessagesSerializer(reflectionHelper);
            _messagesDeserializer = new MessagesDeserializer(reflectionHelper);

            _responser = new Responser(reflectionHelper, receiveDispatcher);

            MessageAwaiters = new ConcurrentDictionary<short, TaskCompletionSource<object>>();
        }

        private volatile bool _alreadyStarted;
        public void Start()
        {
            if (_alreadyStarted)
                return;

            _alreadyStarted = true;

            //we need to clear the SynchronisationContext
            _ = Task.Run(ReadChannelAsync);
        }

        private async Task ReadChannelAsync()
        {
            var reader = Channel.ResponsesChannel.Reader;

            await foreach (var response in reader.ReadAllAsync())
            {
                var data = response.Bytes;

                _receiveMessageAssembler.Enqueue(data);

                while (true)
                {
                    var message = _receiveMessageAssembler.DequeueOrNull();

                    if (message == null)
                        break;

                    _ = NewMessageReceivedAsync(message);
                }
            }
        }


        private async Task NewMessageReceivedAsync(MemoryStream stream)
        {
            //No need to wait for this message, we can start handling next immediately.
            await Task.Yield();

            var deserialized = _messagesDeserializer.Deserialize(stream);

            if (!deserialized.IsSuccessful)
            {
                var error = deserialized.ErrorMessageOrNull;

                var result = _responser.CreateFatalFailedResponseMessage(error, 0, 0);

                await SendMessageAsync(result);

                if (deserialized.NeedToDisconnect)
                    Disconnect();

                return;
            }

            var message = deserialized.MessageOrNull;
            var msgType = deserialized.MessageOrNull.MessageType;
            var askId = deserialized.MessageOrNull.AskId;

            if (msgType == TntMessageType.RequestMessage)
            {
                var response = await _responser.CreateResponseAsync(deserialized.MessageOrNull);
                await SendMessageAsync(response);
            }
            else if(msgType == TntMessageType.PingMessage)
            {
                var response = _responser.CreatePingResponse(deserialized.MessageOrNull);
                await SendMessageAsync(response);
            }
            else //no need to response
            {
                switch (msgType)
                {
                    case TntMessageType.PingResponseMessage:
                    case TntMessageType.SuccessfulResponseMessage:

                        //remove awaiter
                        if (MessageAwaiters.TryRemove(askId, out var smessageAwaiter))
                        {
                            smessageAwaiter.SetResult(message.Result);
                        }

                        break;
                    case TntMessageType.FailedResponseMessage:
                        
                        //remove awaiter with an error
                        if (MessageAwaiters.TryRemove(askId, out var fmessageAwaiter))
                        {
                            var error = (ErrorMessage)message.Result;
                            fmessageAwaiter.SetException(error.Exception);
                        }

                        break;
                    case TntMessageType.FatalFailedResponseMessage:

                        //remove awaiter with an error and disconnect
                        if (MessageAwaiters.TryRemove(askId, out var ffmessageAwaiter))
                        {
                            var error = (ErrorMessage)message.Result;
                            ffmessageAwaiter.SetException(error.Exception);
                        }

                        Disconnect();

                        break;
                    default:
                        break;
                }
            }
        }

        public async Task SendMessageAsync(NewTntMessage message)
        {
            var serialized = _messagesSerializer.SerializeTntMessage(message);
            await Channel.WriteAsync(serialized.ToArray());
        }

        public void SendMessage(NewTntMessage message)
        {
            var serialized = _messagesSerializer.SerializeTntMessage(message);
            Channel.Write(serialized.ToArray());
        }

        public void Disconnect()
        {
            Channel.Disconnect();
        }

        public void Say(int messageId, object[] values)
        {
            short newId;

            unchecked
            {
                newId = _maxAskId++;
            }

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new NewTntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = TntMessageType.RequestMessage,
                Result = values,
            };

            SendMessage(message);
            //var message = _messagesSerializer.SerializeSayMessage((short)messageId, values);
            //TcpChannel.Write(message.ToArray());
        }

        public T Ask<T>(int messageId, object[] values)
        {
            short newId;

            unchecked
            {
                newId = _maxAskId++;
            }

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new NewTntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = TntMessageType.RequestMessage,
                Result = values,
            };

            SendMessage(message);

            if (awaiter.Wait(_maxAnsDelay))
                return (T)awaiter.Result;

            else throw new CallTimeoutException((short)messageId, newId);
        }


        public Task<object> GetAsyncMessageAwaiter(short askId)
        {
            var tks = new TaskCompletionSource<object>();

            if (MessageAwaiters.TryAdd(askId, tks))
                return tks.Task;

            else throw new Exception("Same askId was already added");
        }

        public void SetIncomeAskCallHandler<T>(int messageId, Func<object[], T> callback)
        {
            _reflectionHelper.SetIncomeAskCallHandler(messageId, callback);
        }
        public void SetIncomeSayCallHandler(int messageId, Action<object[]> callback)
        {
            _reflectionHelper.SetIncomeSayCallHandler(messageId, callback);
        }
        public void Unsubscribe(int messageId)
        {
            _reflectionHelper.Unsubscribe(messageId);
        }
    }
}
