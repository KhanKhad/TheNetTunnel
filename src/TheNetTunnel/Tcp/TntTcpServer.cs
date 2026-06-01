using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TheNetTunnel.Api;
using TheNetTunnel.Presentation;

namespace TheNetTunnel.Tcp
{
    public class TntTcpServer<TContract> : IChannelServer<TContract> where TContract : class
    {
        private IPEndPoint IPEndPoint;
        private TcpListener _tcpListener;

        private volatile int _maxId;

        private ConcurrentDictionary<int, IConnection<TContract>> _clients;
        private ConcurrentDictionary<int, IConnection<TContract>> _restrictedClients;

        public int ConnectionsCount => _clients.Count;

        public bool IsListening => _alreadyStarted;

        private readonly ContractBuilder<TContract> _connectionBuilder;

        private TaskCompletionSource<IConnection<TContract>> _waitForAClientTaskSource;

        private CancellationTokenSource _internalWorkCts;
        private Task _internalWorkAsync;
        private int _maxConnections;

        public TntTcpServer(ContractBuilder<TContract> channelBuilder, IPEndPoint endPoint, int maxConnections)
        {
            IPEndPoint = endPoint;

            _connectionBuilder = channelBuilder;

            _tcpListener = new TcpListener(endPoint);

            _clients = new ConcurrentDictionary<int, IConnection<TContract>>();
            _restrictedClients = new ConcurrentDictionary<int, IConnection<TContract>>();

            _waitForAClientTaskSource = new TaskCompletionSource<IConnection<TContract>>();
            _maxConnections = maxConnections;
        }

        private volatile bool _alreadyStarted;
        public void Start()
        {
            if (_alreadyStarted)
                return;

            _alreadyStarted = true;

            _tcpListener.Start();

            _internalWorkCts = new CancellationTokenSource();
            _internalWorkAsync = Task.Run(async () => await InternalStartAsync(_internalWorkCts.Token));
        }

        public Task<IConnection<TContract>> WaitForAClient()
        {
            return _waitForAClientTaskSource.Task;
        }

        private async Task InternalStartAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync(token).ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                        break;

                    await PrepareConnection(tcpClient).ConfigureAwait(false);
                }
                catch { }
            }
        }

        private async Task PrepareConnection(TcpClient tcpClient)
        {
            var newId = _maxId++;

            var tntTcpClient = new TntTcpClient(tcpClient)
            {
                ConnectionId = newId
            };

            tntTcpClient.OnDisconnect += TntTcpClient_OnDisconnect;

            IConnection<TContract> connection;

            if (_maxConnections > 0 && _clients.Count >= _maxConnections)
            {
                connection = await _connectionBuilder.UseChannel(tntTcpClient).BuildAsync(true).ConfigureAwait(false);
                _restrictedClients.TryAdd(newId, connection);
            }
            else
            {
                connection = await _connectionBuilder.UseChannel(tntTcpClient).BuildAsync().ConfigureAwait(false);
                _clients.TryAdd(newId, connection);
                _waitForAClientTaskSource.TrySetResult(connection);
                _waitForAClientTaskSource = new TaskCompletionSource<IConnection<TContract>>();
            }            
        }

        private void TntTcpClient_OnDisconnect(object arg1, ErrorMessage arg2)
        {
            var client = (TntTcpClient)arg1;

            if (_clients.TryRemove(client.ConnectionId, out var connection))
                Disconnected?.Invoke(this, new ClientDisconnectEventArgs<TContract>(connection, arg2));

            else if (_restrictedClients.TryRemove(client.ConnectionId, out connection))
                Disconnected?.Invoke(this, new ClientDisconnectEventArgs<TContract>(connection, arg2));

            connection?.Dispose();
        }

        public void ClientDisconnected(int id)
        {
            if (_clients.TryRemove(id, out var clientObject))
                clientObject.Dispose();
        }


        public event Action<object, ClientDisconnectEventArgs<TContract>> Disconnected;

        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            _internalWorkCts.Cancel();
            _internalWorkAsync.Wait();

            _internalWorkCts.Dispose();

            _waitForAClientTaskSource.TrySetCanceled();
            _tcpListener.Stop();

            var clients = _clients.Values;
            foreach (var client in clients)
                client.Dispose();

            var restrictedClients = _restrictedClients.Values;
            foreach (var client in restrictedClients)
                client.Dispose();

            _connectionBuilder.Dispose();
        }

        public IEnumerable<IConnection<TContract>> GetAllConnections()
        {
            return _clients.Values;
        }
    }
}
