using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Local;
using TNT.Core.Presentation;
using TNT.Core.Transport;

namespace TNT.Core.Tcp
{
    public class TcpChannel : IChannel
    {
        private bool _wasConnected = false;

        private bool allowReceive = false;

        /// <summary>
        /// Actualy bool type. 
        /// </summary>
        private int disconnectIsHandled = 0;
        private CancellationToken _disconnectCancellationToken;
        private CancellationTokenSource _disconnectToken;

        private bool readWasStarted = false;
        private int _bytesReceived = 0;
        private int _bytesSent;

        public TcpChannel(IPAddress address, int port) : this(new TcpClient(new IPEndPoint(address, port)))
        {

        }

        public TcpChannel(TcpClient client)
        {
            Client = client;
            _wasConnected = client.Connected;
            SetEndPoints();
        }

        public TcpChannel()
        {
            Client = new TcpClient();
        }

        public int BytesReceived => _bytesReceived;

        public int BytesSent => _bytesSent;

        public string RemoteEndpointName { get; private set; }
        public string LocalEndpointName { get; private set; }

        public bool IsConnected => Client != null && Client.Connected;

        /// <summary>
        /// Can LClient-user can handle messages now?.
        /// </summary>
        public bool AllowReceive
        {
            get => allowReceive;
            set
            {
                if (allowReceive == value)
                    return;
                allowReceive = value;
                if (value)
                {
                    if (!readWasStarted)
                    {
                        readWasStarted = true;
                        _buffer = new byte[Client.ReceiveBufferSize];
                        _disconnectToken = new CancellationTokenSource();
                        _disconnectCancellationToken = _disconnectToken.Token;
                        //start async read operation.
                        //IOException
                        _ = ReadAsync(_disconnectToken.Token);
                    }
                }
                if (!value)
                {
                    throw new InvalidOperationException("Receiving cannot be stoped");
                }
            }
        }


        public TcpClient Client { get; }

        public event Action<object, byte[]> OnReceive;
        public event Action<object, ErrorMessage> OnDisconnect;
        private byte[] _buffer;
        public async Task ConnectAsync(IPEndPoint endPoint)
        {
            await Client.ConnectAsync(endPoint.Address, endPoint.Port);
            _wasConnected = IsConnected;
            AllowReceive = true;
            SetEndPoints();
        }
        public void Connect(IPEndPoint endPoint)
        {
            this.Client.Connect(endPoint);
            _wasConnected = IsConnected;
            AllowReceive = true;
            SetEndPoints();
        }

        public void DisconnectBecauseOf(ErrorMessage errorOrNull)
        {
            //Thread race state. 
            //AsyncWrite, AsyncRead and main disconnect reason are in concurrence

            if (Interlocked.CompareExchange(ref disconnectIsHandled, 1, 0) == 1)
                return;

            allowReceive = false;
            _disconnectToken.Cancel();
            _disconnectToken.Dispose();
            _disconnectToken = null;
            if (Client.Connected)
            {
                try
                {
                    Client.Close();
                }
                catch
                {
                    /* ignored*/
                }
            }
            OnDisconnect?.Invoke(this, errorOrNull);
        }

        public void Disconnect()
        {
            DisconnectBecauseOf(null);
        }

        /// <summary>
        /// Writes the data to underlying channel
        /// </summary>
        ///<exception cref="ConnectionIsLostException"></exception>
        ///<exception cref="ArgumentNullException"></exception>
        public async Task WriteAsync(byte[] data, int offset, int length)
        {
            if (!_wasConnected)
                throw new ConnectionIsNotEstablishedYet("tcp channel was not connected yet");

            if (!Client.Connected)
            {
                DisconnectBecauseOf(new ErrorMessage()
                {
                    ErrorType = Exceptions.Remote.ErrorType.ConnectionAlreadyLost,
                    AdditionalExceptionInformation = "Connection already lost"
                });
                return;
            }

            try
            {
                var networkStream = Client.GetStream();
                //According to msdn, the WriteAsync call is thread-safe.
                //No need to use lock
                await networkStream.WriteAsync(data, offset, length, _disconnectCancellationToken).ConfigureAwait(false);
                Interlocked.Add(ref _bytesSent, length);
            }
            catch (Exception e)
            {
                Disconnect();
                throw new ConnectionIsLostException(innerException: e,
                    message: "Write operation was failed");
            }
        }

        public void Write(byte[] data, int offset, int length)
        {
            if (!_wasConnected)
                throw new ConnectionIsNotEstablishedYet("tcp channel was not connected yet");

            if (!Client.Connected)
            {
                DisconnectBecauseOf(new ErrorMessage()
                {
                    ErrorType = Exceptions.Remote.ErrorType.ConnectionAlreadyLost,
                    AdditionalExceptionInformation = "Connection already lost"
                });
                return;
            }

            try
            {
                var networkStream = Client.GetStream();
                //According to msdn, the WriteAsync call is thread-safe.
                //No need to use lock
                _ = WriteAsync(data, offset, length);

                Interlocked.Add(ref _bytesSent, length);
            }
            catch (Exception e)
            {
                Disconnect();
                throw new ConnectionIsLostException(innerException: e,
                    message: "Write operation was failed");
            }
        }

        private async Task ReadAsync(CancellationToken token)
        {
            try
            {
                var networkStream = Client.GetStream();
                while (true)
                {
                    if (!Client.Connected)
                        return;

                    var bytesToRead = await networkStream.ReadAsync(_buffer, token).ConfigureAwait(false);
                    if (bytesToRead == 0)
                        throw new InvalidOperationException("Socket is closed");

                    unchecked
                    {
                        _bytesReceived += bytesToRead;
                    }

                    var readed = _buffer.AsSpan(0, bytesToRead).ToArray();

                    OnReceive?.Invoke(this, readed);
                }
            }
            catch
            {
                Disconnect();
            }
        }

        private void SetEndPoints()
        {
            if (!IsConnected) return;

            RemoteEndpointName = EndPointToText(Client.Client.RemoteEndPoint);
            LocalEndpointName  = EndPointToText(Client.Client.LocalEndPoint);
        }

        private string EndPointToText(EndPoint endPoint)
        {
            var endPointText = endPoint.ToString();
            var resultChars = new char[endPointText.Length];
            var forbiddenSymbols = new [] { 'f', '[', ']', ':'};

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
    }
}
