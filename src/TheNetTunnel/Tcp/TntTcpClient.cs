using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TheNetTunnel.Diagnostics;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Exceptions.Remote;
using TheNetTunnel.Presentation;
using TheNetTunnel.Transport;


namespace TheNetTunnel.Tcp
{
    public class TntTcpClient : IChannel
    {
        private TcpClient Client;
        private IPEndPoint IPEndPoint;

        private int _bytesReceived;
        private int _bytesSent;

        public Channel<TcpData> ResponsesChannel { get; }
        public string RemoteEndpointName { get; private set; }
        public string LocalEndpointName { get; private set; }

        public event Action<object, ErrorMessage> OnDisconnect;

        public bool IsConnected => Client.Connected;

        public int BytesReceived => Volatile.Read(ref _bytesReceived);

        public int BytesSent => Volatile.Read(ref _bytesSent);

        public int ConnectionId;

        private Task _internalReadAsync;
        private CancellationTokenSource _internalReadCts;
        public TntTcpClient(IPEndPoint endPoint) : this()
        {
            Client = new TcpClient();
            IPEndPoint = endPoint;
        }

        public TntTcpClient(TcpClient client) : this()
        {
            Client = client;
        }

        private TntTcpClient()
        {
            ResponsesChannel = Channel.CreateUnbounded<TcpData>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = true,
            });
        }

        private volatile bool _alreadyStarted;
        public void Start()
        {
            if (_alreadyStarted)
                return;
            _alreadyStarted = true;

            if (!Client.Connected)
                Client.Connect(IPEndPoint.Address, IPEndPoint.Port);

            InternalStart();
        }


        public async Task StartAsync()
        {
            if (_alreadyStarted)
                return;
            _alreadyStarted = true;

            if (!Client.Connected)
                await Client.ConnectAsync(IPEndPoint.Address, IPEndPoint.Port).ConfigureAwait(false);

            InternalStart();
        }

        private void InternalStart()
        {
            SetEndPoints();

            Client.NoDelay = true;
            Client.Client.Blocking = false;

            _internalReadCts = new CancellationTokenSource();
            _internalReadAsync = Task.Run(async () => await InternalReadAsync(_internalReadCts.Token));
        }

        private const int MinReceiveChunkSize = 4 * 1024;
        private const int MaxReceiveChunkSize = 64 * 1024;

        private async Task InternalReadAsync(CancellationToken token)
        {
            var socket = Client.Client;
            ErrorMessage disconnectReason = null;

            while (!token.IsCancellationRequested && Client.Connected)
            {
                byte[] buffer = null;
                var handedOff = false;
                try
                {
                    // Rent by the actual backlog size: idle connections mostly receive
                    // small frames, and renting a fixed 64K for each of them keeps
                    // large buckets of ArrayPool populated forever.
                    var available = socket.Available;
                    var chunkSize = available < MinReceiveChunkSize ? MinReceiveChunkSize
                        : available > MaxReceiveChunkSize ? MaxReceiveChunkSize
                        : available;

                    buffer = ArrayPool<byte>.Shared.Rent(chunkSize);

                    var bytesToRead = await socket.ReceiveAsync(buffer, SocketFlags.None, token).ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                        break;

                    if (bytesToRead == 0)
                    {
                        disconnectReason = new ErrorMessage(0, 0,
                            ErrorType.ConnectionAlreadyLost,
                            "Remote endpoint closed the connection");
                        break;
                    }

                    Interlocked.Add(ref _bytesReceived, bytesToRead);

                    var data = new TcpData()
                    {
                        Bytes = buffer,
                        Length = bytesToRead,
                        Pooled = true,
                        Sender = this,
                    };

                    await ResponsesChannel.Writer.WriteAsync(data, token).ConfigureAwait(false);
                    handedOff = true;
                }
                catch (OperationCanceledException)
                {
                    // Expected when the read loop is being stopped.
                    break;
                }
                catch (Exception e)
                {
                    // A receive error means the connection is unusable: leave the
                    // loop instead of retrying, otherwise a dead socket would make
                    // ReceiveAsync fail in a tight spin.
                    TntLog.Warning(nameof(TntTcpClient), "Error while receiving data from socket", e);
                    disconnectReason = new ErrorMessage(0, 0,
                        ErrorType.ConnectionAlreadyLost,
                        $"Connection lost while receiving data: {e.Message}");
                    break;
                }
                finally
                {
                    if (buffer != null && !handedOff)
                        ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            // If the loop ended because the remote side dropped (not because we are
            // disconnecting deliberately), complete the responses channel and notify
            // OnDisconnect subscribers. waitForReadLoop is false: we are the read loop.
            if (!token.IsCancellationRequested)
                DisconnectCore(disconnectReason ?? new ErrorMessage(0, 0,
                    ErrorType.ConnectionAlreadyLost,
                    "Tcp connection is lost"), waitForReadLoop: false);
        }

        public async Task WriteAsync(ReadOnlyMemory<byte> data)
        {
            if (!Client.Connected)
            {
                if(!_alreadyStarted)
                    throw new ConnectionIsNotEstablishedYet("tcp channel is not connected yet");

                throw new ConnectionIsLostException("tcp channel is not connected");
            }

            // Writes are not synchronized here: the Interlocutor send loop is the
            // only writer, so frames never interleave on the wire.
            try
            {
                var sent = 0;
                while (sent < data.Length)
                    sent += await Client.Client.SendAsync(data.Slice(sent), SocketFlags.None).ConfigureAwait(false);

                Interlocked.Add(ref _bytesSent, data.Length);
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new ConnectionIsLostException("Failed to send data: tcp channel is lost", innerException: ex);
            }
        }

        private void SetEndPoints()
        {
            if (!Client.Connected) return;

            RemoteEndpointName = EndPointToText(Client.Client.RemoteEndPoint);
            LocalEndpointName = EndPointToText(Client.Client.LocalEndPoint);
        }

        private static string EndPointToText(EndPoint endPoint)
        {
            if (endPoint is IPEndPoint ip)
            {
                return ip.AddressFamily == AddressFamily.InterNetworkV6
                    ? $"[{ip.Address}]:{ip.Port}"
                    : $"{ip.Address}:{ip.Port}";
            }

            return endPoint?.ToString() ?? string.Empty;
        }

        private int _disconnected;

        public void DisconnectBecauseOf(ErrorMessage exceptionMessage)
        {
            DisconnectCore(exceptionMessage, waitForReadLoop: true);
        }

        private void DisconnectCore(ErrorMessage exceptionMessage, bool waitForReadLoop)
        {
            if (Interlocked.CompareExchange(ref _disconnected, 1, 0) != 0)
                return;

            _internalReadCts?.Cancel();

            // waitForReadLoop is false when the read loop itself initiates the
            // disconnect — waiting for it from inside would deadlock.
            if (waitForReadLoop && _internalReadAsync != null)
            {
                try { _internalReadAsync.Wait(); }
                catch { }
            }

            _internalReadCts?.Dispose();

            ResponsesChannel.Writer.Complete();
            Client.Dispose();

            OnDisconnect?.Invoke(this, exceptionMessage);
        }

        public void Disconnect()
        {
            DisconnectBecauseOf(null);
        }

        public void Dispose()
        {
            Disconnect();
            OnDisconnect = null;
        }
    }
}
