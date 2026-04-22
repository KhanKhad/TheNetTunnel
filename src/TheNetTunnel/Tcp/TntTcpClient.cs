using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Presentation;
using TheNetTunnel.Transport;


namespace TheNetTunnel.Tcp
{
    public class TntTcpClient : IChannel
    {
        private TcpClient Client;
        private IPEndPoint IPEndPoint;

        private volatile int _bytesReceived;
        private volatile int _bytesSent;

        public Channel<TcpData> ResponsesChannel { get; }
        public string RemoteEndpointName { get; private set; }
        public string LocalEndpointName { get; private set; }

        public event Action<object, ErrorMessage> OnDisconnect;

        public bool IsConnected => Client.Connected;

        public int BytesReceived => _bytesReceived;

        public int BytesSent => _bytesSent;

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
            ResponsesChannel = Channel.CreateUnbounded<TcpData>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = true,
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

        private async Task InternalWriteAsync(CancellationToken token)
        {
            var bufferSize = 1024;
            var socket = Client.Client;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var buffer = new byte[bufferSize];

                    var bytesToRead = await socket.ReceiveAsync(buffer, SocketFlags.None, token);

                    if (bytesToRead == 0 || token.IsCancellationRequested)
                        continue;

                    unchecked
                    {
                        _bytesReceived += bytesToRead;
                    }

                    var readed = buffer.AsSpan(0, bytesToRead).ToArray();

                    var data = new TcpData()
                    {
                        Bytes = readed,
                        Sender = this,
                    };

                    await ResponsesChannel.Writer.WriteAsync(data, CancellationToken.None);
                }
                catch (TaskCanceledException)
                {

                }
                catch(Exception)
                {

                }
            }
        }

        public async Task WriteAsync(byte[] data)
        {
            if (!Client.Connected)
            {
                if(!_alreadyStarted)
                    throw new ConnectionIsNotEstablishedYet("tcp channel is not connected yet");

                throw new ConnectionIsLostException("tcp channel is not connected");
            }

            try
            {
                await Client.Client.SendAsync(new ReadOnlyMemory<byte>(data, 0, data.Length), SocketFlags.None).ConfigureAwait(false);
                _bytesSent += data.Length;
            }
            catch
            {
                Disconnect();
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
            _internalWriteAsync.Wait();
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
