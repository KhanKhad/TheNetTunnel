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
        private NewReflectionHelper _reflectionHelper;

        private MessagesDeserializer _messagesDeserializer;
        private MessagesSerializer _messagesSerializer;

        private ConcurrentDictionary<short, TaskCompletionSource<object>> MessageAwaiters;


        public Responser(NewReflectionHelper reflectionHelper, MessagesSerializer messagesSerializer, TntTcpClient channel)
        {
            Channel = channel;
            _reflectionHelper = reflectionHelper;

            MessageAwaiters = new ConcurrentDictionary<short, TaskCompletionSource<object>>();
            _messagesDeserializer = new MessagesDeserializer(reflectionHelper);
            _receiveMessageAssembler = new ReceivePduQueue();
        }

        public TntTcpClient Channel { get; }

        public void Start()
        {
            _ = ReadChannelAsync();
        }

        public Task<object> GetAsyncMessageAwaiter(short askId)
        {
            var tks = new TaskCompletionSource<object>();

            if (MessageAwaiters.TryAdd(askId, tks))
                return tks.Task;

            else throw new Exception("Same askId was already added");
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

                    await NewMessageReceivedAsync(message);
                }
            }
        }

        private async Task NewMessageReceivedAsync(MemoryStream message)
        {
            var result = _messagesDeserializer.Deserialize(message);

            switch (result.DeserializeResult)
            {
                case DeserializeResults.InternalError:
                case DeserializeResults.ExternalError:

                    var error = result.ErrorMessageOrNull;

                    var errorMessage = _messagesSerializer.SerializeErrorMessage(error);

                    await Channel.WriteAsync(errorMessage.ToArray());

                    if(result.NeedToDisconnect)
                        Channel.Disconnect();

                    break;



                case DeserializeResults.Request:

                    var requestMessage = (RequestMessage)result.MessageOrNull;

                    try
                    {
                        if (requestMessage.AskId.HasValue)
                        {
                            _askSubscribtion.TryGetValue(requestMessage.TypeId, out var askHandler);

                            if (askHandler == null)
                            {
                                var errorMsg = 
                                    new ErrorMessage(
                                        messageId: requestMessage.TypeId,
                                        askId: requestMessage.AskId,
                                        type: ErrorType.ContractSignatureError,
                                        additionalExceptionInformation: $"ask {requestMessage.TypeId} not implemented");
                                
                                var newErrorMessage = _messagesSerializer.SerializeErrorMessage(errorMsg);

                                await Channel.WriteAsync(newErrorMessage.ToArray());
                            }

                            var answer = askHandler.Invoke(requestMessage.Arguments);



                            _messenger.Ans((short)-requestMessage.TypeId, requestMessage.AskId.Value, answer);
                        }
                        else
                        {
                            _saySubscribtion.TryGetValue(requestMessage.TypeId, out var sayHandler);
                            sayHandler?.Invoke(requestMessage.Arguments);
                        }
                    }
                    catch (LocalSerializationException serializationException)
                    {
                        Debug.WriteLine(serializationException.ToString());

                        _messenger.HandleRequestProcessingError(
                            new ErrorMessage(
                                message.TypeId,
                                message.AskId,
                                ErrorType.SerializationError,
                                serializationException.ToString()), true);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"UnhandledUserExceptionError: {e}");

                        _messenger.HandleRequestProcessingError(
                            new ErrorMessage(
                                message.TypeId,
                                message.AskId,
                                ErrorType.UnhandledUserExceptionError,
                                $"UnhandledException: {e.GetBaseException()}"), false);
                    }

                    break;



                case DeserializeResults.Response:

                    var responseMessage = (ResponseMessage)result.MessageOrNull;

                    var askId = responseMessage.AskId;

                    if(MessageAwaiters.TryRemove(askId, out var messageAwaiter))
                    {
                        messageAwaiter.SetResult(responseMessage.Answer);
                    }

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
