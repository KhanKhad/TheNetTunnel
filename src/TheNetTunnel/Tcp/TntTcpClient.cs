using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TheNetTunnel.Diagnostics;
using TheNetTunnel.Exceptions.Local;
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
            string disconnectReason = null;

            while (!token.IsCancellationRequested && Client.Connected)
            {
                byte[] buffer = null;
                var handedOff = false;
                try
                {
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
                        disconnectReason = "Remote endpoint closed the connection";
                        break;
                    }

                    Interlocked.Add(ref _bytesReceived, bytesToRead);

                    var data = new TcpData()
                    {
                        Bytes = buffer,
                        Length = bytesToRead
                    };

                    await ResponsesChannel.Writer.WriteAsync(data, token).ConfigureAwait(false);
                    handedOff = true;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    TntLog.Warning(nameof(TntTcpClient), "Error while receiving data from socket", e);
                    disconnectReason = $"Connection lost while receiving data: {e.Message}";
                    break;
                }
                finally
                {
                    if (buffer != null && !handedOff)
                        ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            ResponsesChannel.Writer.TryComplete(new Exception(disconnectReason));
        }

        public async Task WriteAsync(ReadOnlyMemory<byte> data)
        {
            if (!Client.Connected)
            {
                if(!_alreadyStarted)
                    throw new ConnectionIsNotEstablishedYet("tcp channel is not connected yet");

                throw new ConnectionIsLostException("tcp channel is not connected");
            }

            try
            {
                var sent = 0;
                while (sent < data.Length)
                    sent += await Client.Client.SendAsync(data[sent..], SocketFlags.None).ConfigureAwait(false);

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
            if (Interlocked.CompareExchange(ref _disconnected, 1, 0) != 0)
                return;

            _internalReadCts?.Cancel();

            if (_internalReadAsync != null)
            {
                try { _internalReadAsync.Wait(); }
                catch { }
            }

            _internalReadCts?.Dispose();

            ResponsesChannel.Writer.TryComplete();
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
