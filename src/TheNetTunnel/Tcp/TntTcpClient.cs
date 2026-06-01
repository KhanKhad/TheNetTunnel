using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

        private Task _internalWriteAsync;
        private CancellationTokenSource _internalWriteCts;
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
            // AllowSynchronousContinuations is intentionally left off: with it on,
            // the reader's continuation (Interlocutor.ReadChannelAsync) could run
            // inline on this socket-read task. If that continuation then triggered
            // a disconnect, DisconnectBecauseOf would call _internalWriteAsync.Wait()
            // on the very task executing it — a self-deadlock.
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

            _internalWriteCts = new CancellationTokenSource();
            _internalWriteAsync = Task.Run(async () => await InternalWriteAsync(_internalWriteCts.Token));
        }

        private const int ReceiveBufferSize = 64 * 1024;

        private async Task InternalWriteAsync(CancellationToken token)
        {
            var socket = Client.Client;

            while (!token.IsCancellationRequested && Client.Connected)
            {
                // Rent a fresh buffer per read: it is handed over to the channel
                // consumer (Interlocutor), which returns it once the payload has
                // been copied out. This avoids a per-read managed allocation.
                var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);
                var handedOff = false;
                try
                {
                    var bytesToRead = await socket.ReceiveAsync(buffer, SocketFlags.None, token).ConfigureAwait(false);

                    if (bytesToRead == 0 || token.IsCancellationRequested)
                        continue;

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
                }
                catch (Exception e)
                {
                    TntLog.Warning(nameof(TntTcpClient), "Error while receiving data from socket", e);
                }
                finally
                {
                    if (!handedOff)
                        ArrayPool<byte>.Shared.Return(buffer);
                }
            }
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
                await Client.Client.SendAsync(data, SocketFlags.None).ConfigureAwait(false);
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
            var endPointText = endPoint.ToString();
            var resultChars = new char[endPointText.Length];
            var forbiddenSymbols = new[] { 'f', '[', ']', ':' };

            for (var i = 0; i < endPointText.Length; i++)
            {
                if (endPointText[i] == ']')
                {
                    resultChars[i] = ':';
                }
                else if (!forbiddenSymbols.Contains(endPointText[i]))
                {
                    resultChars[i] = endPointText[i];
                }
            }
            return new string(resultChars);
        }

        private int _disconnected;

        public void DisconnectBecauseOf(ErrorMessage exceptionMessage)
        {
            if (Interlocked.CompareExchange(ref _disconnected, 1, 0) != 0)
                return;

            _internalWriteCts.Cancel();

            // The read loop winds down on cancellation; faults/cancellation observed
            // here are expected during shutdown and must not escape Disconnect.
            try { _internalWriteAsync.Wait(); }
            catch { }

            _internalWriteCts.Dispose();

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
