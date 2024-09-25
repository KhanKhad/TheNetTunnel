using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Local;
using TNT.Core.Exceptions.Remote;
using TNT.Core.New.Tcp;
using TNT.Core.Presentation;
using TNT.Core.Transport;

namespace TNT.Core.New
{
    public class Responser
    {
        private readonly ConcurrentDictionary<int, Action<object[]>> _saySubscribtion
            = new ConcurrentDictionary<int, Action<object[]>>();

        private readonly ConcurrentDictionary<int, Func<object[], object>> _askSubscribtion
           = new ConcurrentDictionary<int, Func<object[], object>>();

        private readonly ReceivePduQueue _receiveMessageAssembler;


        public Responser(TntTcpClient channel)
        {
            Channel = channel;
            _receiveMessageAssembler = new ReceivePduQueue();
        }

        public TntTcpClient Channel { get; }

        public void Start()
        {
            _ = ReadChannelAsync();
        }

        public Task<object> GetAsyncMessageAwaiter(int askId)
        {
            return Task.FromResult(new object());
        }

        public object GetMessageAwaiter(int askId)
        {
            return new object();
        }


        private async Task ReadChannelAsync()
        {
            var reader = Channel.ResponsesChannel.Reader;

            await foreach (var response in reader.ReadAllAsync().ConfigureAwait(false)) 
            {
                var data = response.Bytes;

                _receiveMessageAssembler.Enqueue(data);

                while (true)
                {
                    var message = _receiveMessageAssembler.DequeueOrNull();
                    
                    if (message == null)
                        break;

                    NewMessageReceived(message);
                }
            }
        }

        private void NewMessageReceived(MemoryStream message)
        {
            var result = MessagesDeserializer.Deserialize(message);


            switch (result.DeserializeResult)
            {
                case DeserializeResults.InternalError:
                    break;
                case DeserializeResults.ExternalError:
                    break;
                case DeserializeResults.Request:
                    break;
                case DeserializeResults.Response:
                    break;
            }
        }

        public void SetIncomeAskCallHandler<T>(int messageId, Func<object[], T> callback)
        {
            _askSubscribtion.TryAdd(messageId, (args) => callback(args));
        }

        public void SetIncomeSayCallHandler(int messageId, Action<object[]> callback)
        {
            _saySubscribtion.TryAdd(messageId, callback);
        }
    }
}
