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
using TNT.Core.Presentation;

namespace TNT.Core.Tcp
{
    public class TntTcpServer<TContract> : IChannelServer<TContract> where TContract : class
    {
        private IPEndPoint IPEndPoint;
        private TcpListener _tcpListener;

        private volatile int _maxId;

        private ConcurrentDictionary<int, IConnection<TContract>> _clients;

        public int ConnectionsCount => _clients.Count;

        public bool IsListening => _alreadyStarted;

        private readonly ContractBuilder<TContract> _connectionBuilder;

        private TaskCompletionSource<IConnection<TContract>> _waitForAClientTaskSource;

        public TntTcpServer(ContractBuilder<TContract> channelBuilder, IPEndPoint endPoint)
        {
            IPEndPoint = endPoint;

            _connectionBuilder = channelBuilder;

            _tcpListener = new TcpListener(endPoint);

            _clients = new ConcurrentDictionary<int, IConnection<TContract>>();

            _waitForAClientTaskSource = new TaskCompletionSource<IConnection<TContract>>();
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

        public Task<IConnection<TContract>> WaitForAClient(bool newClient = false)
        {
            if (newClient)
            {
                //_waitForAClientTaskSource.
                _waitForAClientTaskSource = new TaskCompletionSource<IConnection<TContract>>();
            }

            return _waitForAClientTaskSource.Task;
        }

        private async Task InternalStartAsync()
        {
            while (!_disposed)
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                var newId = _maxId++;

                var tntTcpClient = new TntTcpClient(tcpClient)
                {
                    ConnectionId = newId
                };

                tntTcpClient.OnDisconnect += TntTcpClient_OnDisconnect;

                var connection = await _connectionBuilder.UseChannel(tntTcpClient).BuildAsync();

                var beforeConnectEventArgs = new BeforeConnectEventArgs<TContract>(connection);
                BeforeConnect?.Invoke(this, beforeConnectEventArgs);

                if (!beforeConnectEventArgs.AllowConnection)
                {
                    connection.Dispose();
                    return;
                }

                _clients.TryAdd(newId, connection);

                AfterConnect?.Invoke(this, connection);

                _waitForAClientTaskSource.TrySetResult(connection);
            }
        }

        private void TntTcpClient_OnDisconnect(object arg1, ErrorMessage arg2)
        {
            var client = (TntTcpClient)arg1;

            if (_clients.TryRemove(client.ConnectionId, out var connection))
                Disconnected?.Invoke(this, new ClientDisconnectEventArgs<TContract>(connection, arg2));

            client.Dispose();
        }

        public void ClientDisconnected(int id)
        {
            if (_clients.TryRemove(id, out var clientObject))
                clientObject.Dispose();
        }


        public event Action<object, BeforeConnectEventArgs<TContract>> BeforeConnect;
        public event Action<object, IConnection<TContract>> AfterConnect;
        public event Action<object, ClientDisconnectEventArgs<TContract>> Disconnected;

        private volatile bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _tcpListener.Stop();

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
