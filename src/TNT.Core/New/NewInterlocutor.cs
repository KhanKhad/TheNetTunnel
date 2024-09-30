using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Local;
using TNT.Core.Exceptions.Remote;
using TNT.Core.New.Tcp;
using TNT.Core.Presentation;
using TNT.Core.Tcp;
using TNT.Core.Transport;

namespace TNT.Core.New
{
    public class NewInterlocutor
    {
        public TntTcpClient TcpChannel;
        public Responser _responser;

        private MessagesSerializer _messagesSerializer;
        private MessagesDeserializer _messagesDeserializer;
        private readonly ReceivePduQueue _receiveMessageAssembler;

        private volatile short _maxAskId = 0;
        private readonly int _maxAnsDelay;

        private ConcurrentDictionary<short, TaskCompletionSource<object>> MessageAwaiters;

        public NewInterlocutor(NewReflectionHelper reflectionHelper, TntTcpClient channel, int maxAnsDelay = 3000)
        {
            _maxAnsDelay = maxAnsDelay;

            TcpChannel = channel;

            _receiveMessageAssembler = new ReceivePduQueue();

            _messagesSerializer = new MessagesSerializer(reflectionHelper);
            _messagesDeserializer = new MessagesDeserializer(reflectionHelper);

            _responser = new Responser(reflectionHelper, _messagesSerializer, channel);

            MessageAwaiters = new ConcurrentDictionary<short, TaskCompletionSource<object>>();
        }

        public void Start()
        {
            _ = ReadChannelAsync();
        }

        private async Task ReadChannelAsync()
        {
            var reader = TcpChannel.ResponsesChannel.Reader;

            await foreach (var response in reader.ReadAllAsync().ConfigureAwait(false))
            {
                var data = response.Bytes;

                _receiveMessageAssembler.Enqueue(data);

                while (true)
                {
                    var message = _receiveMessageAssembler.DequeueOrNull();

                    if (message == null)
                        break;

                    _ = NewMessageReceivedAsync(message).ConfigureAwait(false);
                }
            }
        }


        private async Task NewMessageReceivedAsync(MemoryStream message)
        {
            var result = _messagesDeserializer.Deserialize(message);

            if (!result.IsSuccessful)
            {

            }
        }


        public void Say(int messageId, object[] values)
        {
            var message = _messagesSerializer.SerializeSayMessage((short)messageId, values);
            TcpChannel.Write(message.ToArray());
        }

        public T Ask<T>(int messageId, object[] values)
        {
            short newId;

            unchecked
            {
                newId = _maxAskId++;
            }

            var message = _messagesSerializer.SerializeAskMessage((short)messageId, newId, values);
            
            var awaiter = GetAsyncMessageAwaiter(newId);

            TcpChannel.Write(message.ToArray());

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
            _responser.SetIncomeAskCallHandler(messageId, callback);
        }
        public void SetIncomeSayCallHandler(int messageId, Action<object[]> callback)
        {
            _responser.SetIncomeSayCallHandler(messageId, callback);
        }
    }
}
