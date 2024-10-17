using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TNT.Core.Contract;
using TNT.Core.Exceptions.Local;
using TNT.Core.ReceiveDispatching;
using TNT.Core.Transport;

namespace TNT.Core.Presentation
{
    public class Interlocutor : IInterlocutor
    {
        public IChannel Channel;
        public Responser _responser;

        private MessagesSerializer _messagesSerializer;
        private MessagesDeserializer _messagesDeserializer;
        private IDispatcher _receiveDispatcher;
        private readonly ReceivePduQueue _receiveMessageAssembler;

        private int _maxAskId;
        private readonly int _maxAnsDelay;

        private ConcurrentDictionary<int, TaskCompletionSource<object>> MessageAwaiters;

        public Interlocutor(IDispatcher receiveDispatcher, IChannel channel, int maxAnsDelay = 3000)
        {
            _maxAnsDelay = maxAnsDelay;

            Channel = channel;

            _receiveMessageAssembler = new ReceivePduQueue();
            _receiveDispatcher = receiveDispatcher;

            MessageAwaiters = new ConcurrentDictionary<int, TaskCompletionSource<object>>();
        }

        public void Initialize(MethodsDescriptor methodsDescriptor)
        {
            _messagesSerializer = new MessagesSerializer(methodsDescriptor);
            _messagesDeserializer = new MessagesDeserializer(methodsDescriptor);
            _responser = new Responser(methodsDescriptor, _receiveDispatcher);
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

                NewTntMessage result;

                if(deserialized.NeedToDisconnect)
                    result = _responser.CreateFatalFailedResponseMessage(error, error.MessageId, error.AskId);
                else result = _responser.CreateFailedResponseMessage(error, error.MessageId, error.AskId);

                await SendMessageAsync(result).ConfigureAwait(false);

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
            else if (msgType == TntMessageType.PingMessage)
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

            Channel.WriteAsync(serialized.ToArray()).GetAwaiter().GetResult();
        }

        public void Disconnect()
        {
            Channel.Disconnect();
        }

        public void Say(int messageId, object[] values)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new NewTntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = TntMessageType.RequestMessage,
                Result = values,
            };

            SendMessage(message);
        }
        public async Task SayAsync(int messageId, object[] values)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new NewTntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = TntMessageType.RequestMessage,
                Result = values,
            };

            await SendMessageAsync(message);

            var result = await Task.WhenAny(awaiter, Task.Delay(_maxAnsDelay));

            if (result == awaiter)
                await awaiter;

            else throw new CallTimeoutException((short)messageId, newId);
        }
        public T Ask<T>(int messageId, object[] values)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new NewTntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = TntMessageType.RequestMessage,
                Result = values,
            };

            SendMessage(message);

            try
            {
                if (awaiter.Wait(_maxAnsDelay))
                    return (T)awaiter.Result;
            }
            catch(AggregateException ae)
            {
                if(ae.InnerExceptions.Count == 1)
                    throw ae.InnerException;
                else throw;
            }

            throw new CallTimeoutException((short)messageId, newId);
        }

        public async Task<T> AskAsync<T>(int messageId, object[] values)
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new NewTntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = TntMessageType.RequestMessage,
                Result = values,
            };

            await SendMessageAsync(message);

            var result = await Task.WhenAny(awaiter, Task.Delay(_maxAnsDelay));

            if (result == awaiter)
                return (T)await awaiter;

            else throw new CallTimeoutException((short)messageId, newId);
        }

        public Task<object> GetAsyncMessageAwaiter(int askId)
        {
            var tks = new TaskCompletionSource<object>();

            if (MessageAwaiters.TryAdd(askId, tks))
                return tks.Task;

            else throw new Exception("Same askId was already added");
        }
    }
}
