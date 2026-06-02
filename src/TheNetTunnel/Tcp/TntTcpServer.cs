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
using TheNetTunnel.Diagnostics;
using TheNetTunnel.Presentation;

namespace TheNetTunnel.Tcp
{
    public class TntTcpServer<TContract> : IChannelServer<TContract> where TContract : class
    {
        private IPEndPoint IPEndPoint;
        private TcpListener _tcpListener;

        private int _maxId;

        private ConcurrentDictionary<int, IConnection<TContract>> _clients;
        private ConcurrentDictionary<int, IConnection<TContract>> _restrictedClients;

        public int ConnectionsCount => _clients.Count;

        public bool IsListening => _alreadyStarted;

        private readonly ContractBuilder<TContract> _connectionBuilder;

        private readonly Channel<IConnection<TContract>> _acceptedConnections;

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

            _acceptedConnections = Channel.CreateUnbounded<IConnection<TContract>>(new UnboundedChannelOptions()
            {
                SingleWriter = true,
            });
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
            return _acceptedConnections.Reader.ReadAsync().AsTask();
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
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    TntLog.Error(nameof(TntTcpServer<TContract>), "Failed to accept an incoming connection", e);
                }
            }
        }

        private async Task PrepareConnection(TcpClient tcpClient)
        {
            var newId = Interlocked.Increment(ref _maxId);

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
                _acceptedConnections.Writer.TryWrite(connection);
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

            _internalWorkCts?.Cancel();
            _internalWorkAsync?.Wait();

            _internalWorkCts?.Dispose();

            _acceptedConnections.Writer.TryComplete();
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
