﻿using System;
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

        private volatile bool _alreadyStarted;
        public void Start()
        {
            if (_alreadyStarted)
                return;
            _alreadyStarted = true;

            if (!Client.Connected)
                Client.Connect(IPEndPoint.Address, IPEndPoint.Port);

            SetEndPoints();

            _ = InternalStartAsync();
        }


        public async Task StartAsync()
        {
            if (_alreadyStarted)
                return;
            _alreadyStarted = true;

            if (!Client.Connected)
                await Client.ConnectAsync(IPEndPoint.Address, IPEndPoint.Port).ConfigureAwait(false);

            SetEndPoints();

            _ = InternalStartAsync();
        }

        private async Task InternalStartAsync()
        {
            var reader = Client.GetStream();

            var buffer = new byte[Client.ReceiveBufferSize];

            while (!_disconnected && !_disposed)
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

        public void Write(byte[] data)
        {
            if (!Client.Connected)
                throw new ConnectionIsLostException("tcp channel is not connected");

            try
            {
                var networkStream = Client.GetStream();

                networkStream.Write(data);

                _bytesSent += data.Length;
            }
            catch
            {
                Disconnect();
            }
        }

        public async Task WriteAsync(byte[] data)
        {
            if (!Client.Connected)
                throw new ConnectionIsLostException("tcp channel is not connected");

            try
            {
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

        private volatile bool _disconnected;

        public void DisconnectBecauseOf(ErrorMessage exceptionMessage)
        {
            if (_disconnected)
                return;

            _disconnected = true;

            ResponsesChannel.Writer.Complete();
            Client.Dispose();

            OnDisconnect?.Invoke(this, exceptionMessage);
        }

        public void Disconnect()
        {
            DisconnectBecauseOf(null);
        }

        private volatile bool _disposed;
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            Disconnect();

            OnDisconnect = null;
        }
    }
}
