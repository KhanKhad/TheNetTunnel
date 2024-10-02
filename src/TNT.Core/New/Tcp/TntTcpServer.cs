using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TNT.Core.Api;

namespace TNT.Core.New.Tcp
{
    public class TntTcpServer<TContract> : IChannelServer<TContract> where TContract : class
    {
        private IPEndPoint IPEndPoint;
        private TcpListener _tcpListener;

        private volatile int _maxId;

        private ConcurrentDictionary<int, IConnection<TContract>> _clients;

        public Channel<TcpData> ResponsesChannel { get; }

        public int ConnectionsCount => _clients.Count;

        public bool IsListening => _alreadyStarted;

        private readonly PresentationBuilder<TContract> _connectionBuilder;

        public TntTcpServer(PresentationBuilder<TContract> channelBuilder, IPEndPoint endPoint)
        {
            IPEndPoint = endPoint;

            _connectionBuilder = channelBuilder;

            _tcpListener = new TcpListener(endPoint);

            _clients = new ConcurrentDictionary<int, IConnection<TContract>>();

            ResponsesChannel = Channel.CreateBounded<TcpData>(5);
        }

        private volatile bool _alreadyStarted;
        public void Start()
        {
            if (_alreadyStarted)
                return;

            _alreadyStarted = true;

            _tcpListener.Start();

            _ = Task.Run(InternalStartAsync);
        }


        private async Task InternalStartAsync()
        {
            while (!_disposed)
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                var newId = _maxId++;

                var tntTcpClient = new TntTcpClient(tcpClient);

                var connection = _connectionBuilder.UseChannel(tntTcpClient).Build();

                _clients.TryAdd(newId, connection);
            }
        }


        public void ClientDisconnected(int id)
        {
            if (_clients.TryRemove(id, out var clientObject))
                clientObject.Dispose();
        }

        private volatile bool _disposed;

        public event Action<object, BeforeConnectEventArgs<TContract>> BeforeConnect;
        public event Action<object, IConnection<TContract>> AfterConnect;
        public event Action<object, ClientDisconnectEventArgs<TContract>> Disconnected;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            var clients = _clients.Values;
            foreach (var client in clients)
                client.Dispose();
        }

        public IEnumerable<IConnection<TContract>> GetAllConnections()
        {
            return _clients.Values;
        }
    }
}
