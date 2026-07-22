using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TheNetTunnel.Diagnostics;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Exceptions.Remote;
using TheNetTunnel.ReceiveDispatching;
using TheNetTunnel.Transport;

namespace TheNetTunnel.Presentation
{
    public class Interlocutor : IInterlocutor
    {
        public IChannel Channel;
        private Responser _responser;

        private MessagesSerializer _messagesSerializer;
        private MessagesDeserializer _messagesDeserializer;
        private IDispatcher _receiveDispatcher;
        private readonly ReceivePduQueue _receiveMessageAssembler;

        private const int SendQueueCapacity = 256;

        private int _maxAskId;

        private ConcurrentDictionary<int, TaskCompletionSource<object>> MessageAwaiters;

        private readonly Channel<PooledMemoryStream> _sendChannel;
        private TaskCompletionSource _firstPingTks;
        public InterlocutorProperties Properties { get; private set; }
        public Interlocutor(IDispatcher receiveDispatcher, IChannel channel, InterlocutorProperties properties)
        {
            Properties = properties;

            Channel = channel;

            _receiveMessageAssembler = new ReceivePduQueue(properties.MaxFrameLength);
            _receiveDispatcher = receiveDispatcher;

            MessageAwaiters = new ConcurrentDictionary<int, TaskCompletionSource<object>>();

            _firstPingTks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Bounded: a slow or stuck remote endpoint must not let the outgoing
            // queue (and the pooled buffers it holds) grow without limit.
            _sendChannel = System.Threading.Channels.Channel.CreateBounded<PooledMemoryStream>(new BoundedChannelOptions(SendQueueCapacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
            });
        }

        public void Initialize(MethodsDescriptor methodsDescriptor)
        {
            _messagesSerializer = new MessagesSerializer(methodsDescriptor);
            _messagesDeserializer = new MessagesDeserializer(methodsDescriptor);
            _responser = new Responser(methodsDescriptor, _receiveDispatcher);
        }


        private Task _readChannelAsync;
        private Task _pingTaskAsync;
        private Task _sendTaskAsync;
        private CancellationTokenSource _workCts;
        private int _disposed;

        public void Start()
        {
            if (_workCts != null)
                return;

            //we need to clear the SynchronisationContext
            _workCts = new CancellationTokenSource();
            _readChannelAsync = Task.Run(async () => await ReadChannelAsync(_workCts.Token));
            _sendTaskAsync = Task.Run(async () => await SendTaskAsync(_workCts.Token));
        }

        public void StartPinging()
        {
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
                ContractId = Properties.ContractId,
                MessageType = MessageType.HelloMessageRequest,
                Result = Properties.CreateHelloMessage(),
            };

            await SendMessageAsync(message, newId).ConfigureAwait(false);

            try
            {
                var result = await awaiter.WaitAsync(TimeSpan.FromMilliseconds(Properties.DefaultMaxAnsDelay)).ConfigureAwait(false);

                var response = result as HelloMessageResponse;
                return (response?.AvailableForWork ?? false, response?.UnavailabilityReason ?? "Invalid response");
            }
            catch (TimeoutException)
            {
                RemoveAsyncMessageAwaiter(newId);
                return (false, "No response to HelloMessage");
            }
            catch (Exception ex)
            {
                return (false, $"Unknown error:{ex.Message}");
            }
        }

        public async Task PingTaskAsync(CancellationToken token)
        {
            // token is _workCts.Token, no need to consult the field again.
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (Properties.ServerMode)
                    {
                        // Pings start only after the first incoming message, so a client
                        // that connects and never sends anything would otherwise hold the
                        // connection (and a maxConnections slot) forever. Give it the
                        // usual answer delay to start talking, then drop it.
                        try
                        {
                            await _firstPingTks.Task
                                .WaitAsync(TimeSpan.FromMilliseconds(Properties.DefaultMaxAnsDelay), token)
                                .ConfigureAwait(false);
                        }
                        catch (TimeoutException)
                        {
                            Disconnect(new ErrorMessage(0, 0,
                                ErrorType.ConnectionAlreadyLost,
                                "No data was received from the remote endpoint within the handshake timeout"));
                            return;
                        }
                    }

                    var newId = Interlocked.Increment(ref _maxAskId);

                    var pongAwaiter = GetAsyncMessageAwaiter(newId);

                    var pingMessage = new TntMessage()
                    {
                        AskId = newId,
                        MessageId = 0,
                        MessageType = MessageType.PingMessage,
                        ContractId = Properties.ContractId,
                        Result = (short)1,
                    };
                    await SendMessageAsync(pingMessage, newId).ConfigureAwait(false);

                    // A connection that accepts writes but never answers is dead:
                    // drop it if the pong does not arrive in time.
                    try
                    {
                        await pongAwaiter.WaitAsync(TimeSpan.FromMilliseconds(Properties.DefaultMaxAnsDelay), token).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        RemoveAsyncMessageAwaiter(newId);
                        Disconnect(new ErrorMessage(0, newId,
                            ErrorType.ConnectionAlreadyLost,
                            "Ping response was not received in time"));
                        return;
                    }

                    await Task.Delay(Properties.DefaultPingInterval, token).ConfigureAwait(false);
                }
                catch (ConnectionIsLostException e)
                {
                    Disconnect(new ErrorMessage(0, 0,
                        ErrorType.ConnectionAlreadyLost,
                        $"Connection lost while sending ping heartbeat: {e.Message}"));
                }
                catch (OperationCanceledException)
                {
                    // Expected when the ping loop is being stopped.
                }
                catch (Exception e)
                {
                    TntLog.Warning(nameof(Interlocutor), "Ping heartbeat iteration failed", e);
                }
            }
        }

        private async Task ReadChannelAsync(CancellationToken token)
        {
            var reader = Channel.ResponsesChannel.Reader;

            try
            {
                await foreach (var response in reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    try
                    {
                        // Enqueue copies the bytes into its own pooled storage, so the
                        // received buffer can be released back to the pool right after.
                        _receiveMessageAssembler.Enqueue(
                            new ReadOnlySpan<byte>(response.Bytes, 0, response.Length));
                    }
                    finally
                    {
                        if (response.Pooled)
                            ArrayPool<byte>.Shared.Return(response.Bytes);
                    }

                    while (true)
                    {
                        var message = _receiveMessageAssembler.DequeueOrNull();

                        if (message == null)
                            break;

                        _ = NewMessageReceivedAsync(message);
                    }
                }

                // The responses channel completes only when the transport is
                // disconnected. React immediately: cancel pending awaiters instead
                // of letting the callers wait for their timeouts.
                if (!token.IsCancellationRequested)
                    Disconnect(new ErrorMessage(0, 0,
                        ErrorType.ConnectionAlreadyLost,
                        "Transport channel was closed"));
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping the read loop
            }
            catch (InvalidFrameLengthException e)
            {
                Disconnect(new ErrorMessage(0, 0,
                    ErrorType.SerializationError,
                    $"Connection dropped: {e.Message}"));
            }
        }


        private async Task NewMessageReceivedAsync(Stream stream)
        {
            try
            {
                var deserialized = _messagesDeserializer.Deserialize(stream);

                stream.Dispose();

                if (!deserialized.IsSuccessful)
                {
                    var error = deserialized.ErrorMessageOrNull;

                    TntMessage result;

                    if (deserialized.NeedToDisconnect)
                        result = Responser.CreateFatalFailedResponseMessage(error, error.MessageId, error.AskId, Properties.ContractId);
                    else result = Responser.CreateFailedResponseMessage(error, error.MessageId, error.AskId, Properties.ContractId);

                    try
                    {
                        await SendAwaitableMessageAsync(result).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (deserialized.NeedToDisconnect)
                            Disconnect(error);
                    }
                }
                else
                {
                    var message = deserialized.MessageOrNull;
                    var msgType = message.MessageType;
                    var askId = message.AskId;

                    if (message.ContractId != Properties.ContractId)
                    {
                        ErrorMessage error;
                        TntMessage response;

                        if (msgType == MessageType.HelloMessageRequest)
                        {
                            response = Responser.CreateContractIdIsNotSuppertedHelloMessageResponse(deserialized.MessageOrNull);

                            error = new ErrorMessage(0, askId,
                                    ErrorType.HandshakeRejected,
                                    $"Handshake rejected — client does not meet requirements: {(response.Result as HelloMessageResponse).UnavailabilityReason}");
                        }
                        else
                        {
                            error = new ErrorMessage(0, askId,
                                    ErrorType.ContractIdIsNotSupported,
                                    $"Contract id {message.ContractId} is not supported");

                            response = Responser.CreateFailedResponseMessage(error, message.MessageId, askId, Properties.ContractId);
                        }

                        try
                        {
                            await SendAwaitableMessageAsync(response).ConfigureAwait(false);
                        }
                        finally
                        {
                            Disconnect(error);
                        }
                    }
                    else if (msgType == MessageType.RequestMessage)
                    {
                        var response = await _responser.CreateResponseAsync(deserialized.MessageOrNull).ConfigureAwait(false);
                        await SendMessageAsync(response).ConfigureAwait(false);
                    }
                    else if (msgType == MessageType.PingMessage)
                    {
                        var response = Responser.CreatePingResponse(deserialized.MessageOrNull, Properties.ContractId);
                        await SendMessageAsync(response).ConfigureAwait(false);
                    }
                    else if (msgType == MessageType.HelloMessageRequest)
                    {
                        var (needDisconnect, response) = Responser.CreateHelloMessageResponse(Properties, deserialized.MessageOrNull);

                        try
                        {
                            await SendAwaitableMessageAsync(response).ConfigureAwait(false);
                        }
                        finally
                        {
                            if (needDisconnect)
                            {
                                var helloResponse = (HelloMessageResponse)response.Result;
                                Disconnect(new ErrorMessage(0, askId,
                                    ErrorType.HandshakeRejected,
                                    $"Handshake rejected — client does not meet requirements: {helloResponse.UnavailabilityReason}"));
                            }
                            else
                            {
                                _firstPingTks.TrySetResult();
                            }
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
            catch (Exception e)
            {
                TntLog.Error(nameof(Interlocutor), "Unhandled error while processing an incoming message", e);
            }
        }

        public async Task SendAwaitableMessageAsync(TntMessage message)
        {
            var serialized = _messagesSerializer.SerializeTntMessage(message);

            serialized.Tks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await _sendChannel.Writer.WriteAsync(serialized).ConfigureAwait(false);

            await serialized.Tks.Task.ConfigureAwait(false);
        }

        public void SendMessage(TntMessage message, int askId = -1)
        {
            var serialized = _messagesSerializer.SerializeTntMessage(message);

            if (_sendChannel.Writer.TryWrite(serialized))
                return;

            // TryWrite fails when the channel is either full or closed. When full,
            // wait for the send loop to drain it (backpressure); when closed,
            // WriteAsync throws ChannelClosedException.
            try
            {
                _sendChannel.Writer.WriteAsync(serialized).AsTask().GetAwaiter().GetResult();
            }
            catch (ChannelClosedException)
            {
                serialized.Dispose();

                if (askId != -1)
                    RemoveAsyncMessageAwaiter(askId);

                throw new ConnectionIsLostException("Send channel is closed");
            }
        }

        public async Task SendMessageAsync(TntMessage message, int askId = -1)
        {
            var serialized = _messagesSerializer.SerializeTntMessage(message);

            if (_sendChannel.Writer.TryWrite(serialized))
                return;

            // Same semantics as SendMessage, but the backpressure wait is awaited
            // instead of blocking the calling thread.
            try
            {
                await _sendChannel.Writer.WriteAsync(serialized).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                serialized.Dispose();

                if (askId != -1)
                    RemoveAsyncMessageAwaiter(askId);

                throw new ConnectionIsLostException("Send channel is closed");
            }
        }

        private async Task SendTaskAsync(CancellationToken token)
        {
            var reader = _sendChannel.Reader;

            try
            {
                await foreach (var message in reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    if (token.IsCancellationRequested)
                        message.Dispose();

                    else
                    {
                        try
                        {
                            await Channel.WriteAsync(message.GetWrittenMemory()).ConfigureAwait(false);

                            message.Tks?.TrySetResult();
                        }
                        catch (Exception e)
                        {
                            // Only locally generated ask ids may have awaiters: responses
                            // carry the remote side's ask id, which can collide with an
                            // unrelated local one.
                            if (message.IsRequest && MessageAwaiters.TryRemove(message.AskId, out var awaiter))
                                awaiter.SetException(e);

                            message.Tks?.TrySetException(e);
                        }
                        finally
                        {
                            message.Dispose();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping the read loop
            }
            catch (Exception e)
            {
                Disconnect(new ErrorMessage(0, 0,
                    ErrorType.ConnectionAlreadyLost,
                    $"SendTaskAsync Connection dropped: {e.Message}"));
            }
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
                ContractId = Properties.ContractId,
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
                ContractId = Properties.ContractId,
                MessageType = MessageType.RequestMessage,
                Result = values,
            };

            await SendMessageAsync(message, newId).ConfigureAwait(false);

            try
            {
                await awaiter.WaitAsync(TimeSpan.FromMilliseconds(Properties.DefaultMaxAnsDelay)).ConfigureAwait(false);
            }
            catch (TimeoutException)
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
                ContractId = Properties.ContractId,
                MessageType = MessageType.RequestMessage,
                Result = values,
            };

            SendMessage(message, newId);

            try
            {
                if (awaiter.Wait(Properties.DefaultMaxAnsDelay))
                    return (T)awaiter.Result;
            }
            catch (AggregateException ae)
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
                ContractId = Properties.ContractId,
                MessageType = MessageType.RequestMessage,
                Result = values,
            };

            await SendMessageAsync(message, newId).ConfigureAwait(false);

            try
            {
                return (T)await awaiter.WaitAsync(TimeSpan.FromMilliseconds(Properties.DefaultMaxAnsDelay)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                RemoveAsyncMessageAwaiter(newId);
                throw new CallTimeoutException((short)messageId, newId);
            }
        }

        public Task<object> GetAsyncMessageAwaiter(int askId)
        {
            var tks = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (MessageAwaiters.TryAdd(askId, tks))
                return tks.Task;

            else throw new InvalidOperationException("Same askId was already added");
        }

        public void RemoveAsyncMessageAwaiter(int askId)
        {
            MessageAwaiters.TryRemove(askId, out _);
        }

        private void ThrowIfDisconnected()
        {
            // Local copy: the field is checked from user threads while
            // Disconnect/Dispose may run concurrently.
            var workCts = _workCts;

            if (workCts == null || workCts.IsCancellationRequested)
                throw new ConnectionIsLostException("Interlocutor is disconnected");
        }

        public void Disconnect(ErrorMessage error = null)
        {
            _workCts?.Cancel();
            _sendChannel.Writer.TryComplete();
            CancelAllAwaiters();
            Channel.DisconnectBecauseOf(error);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (_workCts == null)
                return;

            Disconnect();

            _firstPingTks.TrySetCanceled();
            await _readChannelAsync.ConfigureAwait(false);

            if (_pingTaskAsync != null)
                await _pingTaskAsync.ConfigureAwait(false);

            await _sendTaskAsync.ConfigureAwait(false);

            if (Properties.DisposeDispatcher)
                await _receiveDispatcher.DisposeAsync().ConfigureAwait(false);

            // _workCts is intentionally neither disposed nor nulled out: it can be
            // read concurrently (ThrowIfDisconnected, Disconnect), and a cancelled
            // CancellationTokenSource without timers holds no resources.
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (_workCts == null)
                return;

            Disconnect();

            _firstPingTks.TrySetCanceled();
            _readChannelAsync.ConfigureAwait(false).GetAwaiter().GetResult();
            _pingTaskAsync?.ConfigureAwait(false).GetAwaiter().GetResult();
            _sendTaskAsync.ConfigureAwait(false).GetAwaiter().GetResult();

            if (Properties.DisposeDispatcher)
                _receiveDispatcher.Dispose();

            // See DisposeAsync: _workCts stays alive on purpose.
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

        /// <summary>
        /// When true, the interlocutor disposes its receive dispatcher together
        /// with itself. Default is true for client connections (the dispatcher is
        /// owned by the connection) and false for server connections (the
        /// dispatcher is shared between connections).
        /// </summary>
        public bool DisposeDispatcher = true;

        public Version MinimalServerVersion;
        public Version MinimalClientVersion;
        public Version ClientVersion;
        public Version ServerVersion;

        public byte ContractId = DefaultContractId;

        public int DefaultMaxAnsDelay;
        public int DefaultPingInterval;

        public int MaxFrameLength = ReceivePduQueue.DefaultMaxFrameLength;
        public const byte DefaultContractId = 255;

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
