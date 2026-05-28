using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Exceptions.Remote;
using TheNetTunnel.ReceiveDispatching;
using TheNetTunnel.Transport;

namespace TheNetTunnel.Presentation
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

        private ConcurrentDictionary<int, TaskCompletionSource<object>> MessageAwaiters;

        public InterlocutorProperties Properties { get; private set; }
        private TaskCompletionSource _firstRequestTks;
        public Interlocutor(IDispatcher receiveDispatcher, IChannel channel, InterlocutorProperties properties)
        {
            Properties = properties;

            Channel = channel;

            _receiveMessageAssembler = new ReceivePduQueue();
            _receiveDispatcher = receiveDispatcher;

            MessageAwaiters = new ConcurrentDictionary<int, TaskCompletionSource<object>>();
            _firstRequestTks = new TaskCompletionSource();
        }

        public void Initialize(MethodsDescriptor methodsDescriptor)
        {
            _messagesSerializer = new MessagesSerializer(methodsDescriptor);
            _messagesDeserializer = new MessagesDeserializer(methodsDescriptor);
            _responser = new Responser(methodsDescriptor, _receiveDispatcher);
        }


        private Task _readChannelAsync;
        private Task _pingTaskAsync;
        private CancellationTokenSource _workCts;

        public void Start()
        {
            if (_workCts != null)
                return;

            //we need to clear the SynchronisationContext
            _workCts = new CancellationTokenSource();
            _readChannelAsync = Task.Run(async () => await ReadChannelAsync(_workCts.Token));
            _pingTaskAsync = Task.Run(async () => await PingTaskAsync(_workCts.Token));
        }

        public async Task<(bool AvailableForWork, string UnavailabilityReason)> SendHelloMessageAsync()
        {
            var newId = Interlocked.Increment(ref _maxAskId);

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new TntMessage()
            {
                AskId = newId,
                MessageId = 0,
                MessageType = MessageType.HelloMessageRequest,
                Result = Properties.CreateHelloMessage(),
            };

            await SendMessageAsync(message);

            var result = await Task.WhenAny(awaiter, Task.Delay(Properties.DefaultMaxAnsDelay));

            if (result == awaiter)
            {
                var response = (await awaiter) as HelloMessageResponse;
                return (response?.AvailableForWork ?? false, response?.UnavailabilityReason ?? "Invalid response");
            }
            else
            {
                RemoveAsyncMessageAwaiter(newId);
                return (false, "No response to HelloMessage");
            }
        }

        public async Task PingTaskAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _workCts != null && !_workCts.IsCancellationRequested)
            {
                try
                {
                    if (Properties.ServerMode)
                        await _firstRequestTks.Task;

                    var pingMessage = new TntMessage()
                    {
                        AskId = Interlocked.Increment(ref _maxAskId),
                        MessageId = 0,
                        MessageType = MessageType.PingMessage,
                        Result = (short)1,
                    };
                    await SendMessageAsync(pingMessage);
                    await Task.Delay(Properties.DefaultPingInterval, token);
                }
                catch(ConnectionIsLostException e)
                {
                    Disconnect(new ErrorMessage(0, 0,
                        ErrorType.ConnectionAlreadyLost,
                        $"Connection lost while sending ping heartbeat: {e.Message}"));
                }
                catch
                {

                }
            }
        }

        private async Task ReadChannelAsync(CancellationToken token)
        {
            var reader = Channel.ResponsesChannel.Reader;

            try
            {
                await foreach (var response in reader.ReadAllAsync(token))
                {
                    var data = response.Bytes;

                    _receiveMessageAssembler.Enqueue(data);

                    while (true)
                    {
                        var message = _receiveMessageAssembler.DequeueOrNull();

                        if (message == null)
                            break;

                        _firstRequestTks.TrySetResult();
                        _ = NewMessageReceivedAsync(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping the read loop
            }
        }


        private async Task NewMessageReceivedAsync(Stream stream)
        {
            try
            {
                //No need to wait for this message, we can start handling next immediately.
                await Task.Yield();

                var deserialized = _messagesDeserializer.Deserialize(stream);

                stream.Dispose();

                if (!deserialized.IsSuccessful)
                {
                    var error = deserialized.ErrorMessageOrNull;

                    TntMessage result;

                    if (deserialized.NeedToDisconnect)
                        result = _responser.CreateFatalFailedResponseMessage(error, error.MessageId, error.AskId);
                    else result = _responser.CreateFailedResponseMessage(error, error.MessageId, error.AskId);

                    await SendMessageAsync(result).ConfigureAwait(false);

                    if (deserialized.NeedToDisconnect)
                        Disconnect(error);
                }
                else
                {
                    var message = deserialized.MessageOrNull;
                    var msgType = deserialized.MessageOrNull.MessageType;
                    var askId = deserialized.MessageOrNull.AskId;

                    if (msgType == MessageType.RequestMessage)
                    {
                        var response = await _responser.CreateResponseAsync(deserialized.MessageOrNull);
                        await SendMessageAsync(response);
                    }
                    else if (msgType == MessageType.PingMessage)
                    {
                        var response = _responser.CreatePingResponse(deserialized.MessageOrNull);
                        await SendMessageAsync(response);
                    }
                    else if (msgType == MessageType.HelloMessageRequest)
                    {
                        var (needDisconnect, response) = _responser.CreateHelloMessageResponse(Properties, deserialized.MessageOrNull);

                        await SendMessageAsync(response);

                        if (needDisconnect)
                        {
                            var helloResponse = (HelloMessageResponse)response.Result;
                            Disconnect(new ErrorMessage(0, askId,
                                ErrorType.HandshakeRejected,
                                $"Handshake rejected — client does not meet requirements: {helloResponse.UnavailabilityReason}"));
                        }
                    }
                    else //no need to response
                    {
                        switch (msgType)
                        {
                            case MessageType.PingResponseMessage:
                            case MessageType.SuccessfulResponseMessage:

                                //remove awaiter
                                if (MessageAwaiters.TryRemove(askId, out var smessageAwaiter))
                                {
                                    smessageAwaiter.SetResult(message.Result);
                                }

                                break;
                            case MessageType.FailedResponseMessage:

                                //remove awaiter with an error
                                if (MessageAwaiters.TryRemove(askId, out var fmessageAwaiter))
                                {
                                    var error = (ErrorMessage)message.Result;
                                    fmessageAwaiter.SetException(error.Exception);
                                }

                                break;

                            case MessageType.HelloMessageResponse:
                            {
                                var helloResponse = (HelloMessageResponse)message.Result;

                                if (MessageAwaiters.TryRemove(askId, out var hrmessageAwaiter))
                                    hrmessageAwaiter.SetResult(helloResponse);

                                if (!helloResponse.AvailableForWork)
                                    Disconnect(new ErrorMessage(0, askId,
                                        ErrorType.HandshakeRejected,
                                        $"Server rejected connection: {helloResponse.UnavailabilityReason}"));

                                break;
                            }

                            case MessageType.FatalFailedResponseMessage:
                            {
                                var fatalError = (ErrorMessage)message.Result;

                                if (MessageAwaiters.TryRemove(askId, out var ffmessageAwaiter))
                                    ffmessageAwaiter.SetException(fatalError.Exception);

                                Disconnect(fatalError);

                                break;
                            }

                            case MessageType.DisconnectMessage:
                                Disconnect(new ErrorMessage(0, askId,
                                    ErrorType.ConnectionAlreadyLost,
                                    "Disconnect message received from remote endpoint"));
                                break;

                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
        }

        public async Task SendMessageAsync(TntMessage message)
        {
            using var serialized = _messagesSerializer.SerializeTntMessage(message);

            await Channel.WriteAsync(serialized.GetWrittenMemory());
        }

        public void SendMessage(TntMessage message)
        {
            using var serialized = _messagesSerializer.SerializeTntMessage(message);

            Channel.WriteAsync(serialized.GetWrittenMemory()).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void Say(int messageId, object[] values)
        {
            ThrowIfDisconnected();

            var newId = Interlocked.Increment(ref _maxAskId);

            var message = new TntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = MessageType.RequestMessage,
                Result = values,
            };

            SendMessage(message);
        }
        public async Task SayAsync(int messageId, object[] values)
        {
            ThrowIfDisconnected();

            var newId = Interlocked.Increment(ref _maxAskId);

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new TntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = MessageType.RequestMessage,
                Result = values,
            };

            await SendMessageAsync(message).ConfigureAwait(false);

            var result = await Task.WhenAny(awaiter, Task.Delay(Properties.DefaultMaxAnsDelay));

            if (result == awaiter)
                await awaiter;

            else
            {
                RemoveAsyncMessageAwaiter(newId);
                throw new CallTimeoutException((short)messageId, newId);
            }
        }
        public T Ask<T>(int messageId, object[] values)
        {
            ThrowIfDisconnected();

            var newId = Interlocked.Increment(ref _maxAskId);

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new TntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = MessageType.RequestMessage,
                Result = values,
            };

            SendMessage(message);

            try
            {
                if (awaiter.Wait(Properties.DefaultMaxAnsDelay))
                    return (T)awaiter.Result;
            }
            catch(AggregateException ae)
            {
                RemoveAsyncMessageAwaiter(newId);

                if (ae.InnerExceptions.Count == 1)
                    ExceptionDispatchInfo.Capture(ae.InnerException).Throw();
                throw;
            }

            RemoveAsyncMessageAwaiter(newId);
            throw new CallTimeoutException((short)messageId, newId);
        }

        public async Task<T> AskAsync<T>(int messageId, object[] values)
        {
            ThrowIfDisconnected();

            var newId = Interlocked.Increment(ref _maxAskId);

            var awaiter = GetAsyncMessageAwaiter(newId);

            var message = new TntMessage()
            {
                AskId = newId,
                MessageId = (short)messageId,
                MessageType = MessageType.RequestMessage,
                Result = values,
            };

            await SendMessageAsync(message).ConfigureAwait(false);

            var result = await Task.WhenAny(awaiter, Task.Delay(Properties.DefaultMaxAnsDelay));

            if (result == awaiter)
                return (T)await awaiter;
            else
            {
                RemoveAsyncMessageAwaiter(newId);
                throw new CallTimeoutException((short)messageId, newId);
            }
        }

        public Task<object> GetAsyncMessageAwaiter(int askId)
        {
            var tks = new TaskCompletionSource<object>();

            if (MessageAwaiters.TryAdd(askId, tks))
                return tks.Task;

            else throw new Exception("Same askId was already added");
        }

        public void RemoveAsyncMessageAwaiter(int askId)
        {
            MessageAwaiters.TryRemove(askId, out _);
        }

        private void ThrowIfDisconnected()
        {
            if (_workCts == null || _workCts.IsCancellationRequested)
                throw new ConnectionIsLostException("Interlocutor is disconnected");
        }

        public void Disconnect(ErrorMessage error = null)
        {
            _workCts?.Cancel();
            CancelAllAwaiters();
            Channel.DisconnectBecauseOf(error);
        }

        public async ValueTask DisposeAsync()
        {
            if (_workCts == null)
                return;

            Disconnect();

            _firstRequestTks.TrySetCanceled();
            await _readChannelAsync;
            await _pingTaskAsync;

            _workCts.Dispose();
            _workCts = null;
        }

        public void Dispose()
        {
            if (_workCts == null)
                return;

            Disconnect();

            _firstRequestTks.TrySetCanceled();
            _readChannelAsync.ConfigureAwait(false).GetAwaiter().GetResult();
            _pingTaskAsync.ConfigureAwait(false).GetAwaiter().GetResult();

            _workCts.Dispose();
            _workCts = null;
        }

        private void CancelAllAwaiters()
        {
            foreach (var kvp in MessageAwaiters)
            {
                if (MessageAwaiters.TryRemove(kvp.Key, out var tcs))
                {
                    tcs.TrySetCanceled();
                }
            }
        }
    }

    public class InterlocutorProperties
    {
        public bool Fullmode;
        public bool ServerMode;

        public Version MinimalServerVersion;
        public Version MinimalClientVersion;
        public Version ClientVersion;
        public Version ServerVersion;

        public int DefaultMaxAnsDelay;
        public int DefaultPingInterval;

        public InterlocutorProperties() { }


        public HelloMessageRequest CreateHelloMessage()
        {
            return new HelloMessageRequest()
            {
                MyVersion = ServerMode ? ServerVersion : ClientVersion,
                MinimalVersion = ServerMode ? MinimalClientVersion : MinimalServerVersion,
            };
        }
    }
}
