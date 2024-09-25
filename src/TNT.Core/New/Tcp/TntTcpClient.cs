using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Local;
using TNT.Core.Presentation;
using TNT.Core.Transport;


namespace TNT.Core.New.Tcp
{
    public class TntTcpClient : IDisposable
    {
        private TcpClient Client;
        private IPEndPoint IPEndPoint;

        private volatile int _bytesReceived;
        private volatile int _bytesSent;

        private volatile ChannelStates _state;
        public ChannelStates State => _state;
        public Channel<TcpData> ResponsesChannel { get; }
        public string RemoteEndpointName { get; private set; }
        public string LocalEndpointName { get; private set; }

        public event Action<object, ErrorMessage> OnDisconnect;

        public bool IsConnected => Client.Connected;

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
            ResponsesChannel = Channel.CreateBounded<TcpData>(3);
        }

        public void Start()
        {
            _ = InternalStartAsync();
        }

        public async Task InternalStartAsync()
        {
            if(!Client.Connected)
                await Client.ConnectAsync(IPEndPoint.Address, IPEndPoint.Port).ConfigureAwait(false);

            _state = ChannelStates.Connected;

            SetEndPoints();

            var reader = Client.GetStream();

            var buffer = new byte[Client.ReceiveBufferSize];

            while (_state == ChannelStates.Connected)
            {
                try
                {
                    var bytesToRead = await reader.ReadAsync(buffer).ConfigureAwait(false);

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

                    await ResponsesChannel.Writer.WriteAsync(data);
                }
                catch
                {
                    Disconnect();
                }
            }
        }

        public async Task WriteAsync(byte[] data)
        {
            try
            {
                if (!Client.Connected)
                    throw new ConnectionIsLostException("tcp channel is not connected");

                var networkStream = Client.GetStream();
                //According to msdn, the WriteAsync call is thread-safe.
                //No need to use lock
                await networkStream.WriteAsync(data, CancellationToken.None).ConfigureAwait(false);

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

        private string EndPointToText(EndPoint endPoint)
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

        public void Disconnect()
        {
            if (_state == ChannelStates.Disconnected)
                return;

            if(_state != ChannelStates.Disposed)
                _state = ChannelStates.Disconnected;

            ResponsesChannel.Writer.Complete();
            Client.Dispose();

            OnDisconnect?.Invoke(this, new ErrorMessage());
        }


        public void Dispose()
        {
            if (_state == ChannelStates.Disposed)
                return;

            _state = ChannelStates.Disposed;

            Disconnect();

            OnDisconnect = null;
        }

        public void DisconnectBecauseOf(ErrorMessage exceptionMessage)
        {
            Disconnect();
        }
    }


    public enum ChannelStates
    {
        None,
        Connected,
        Disconnected,
        Disposed
    }
}
