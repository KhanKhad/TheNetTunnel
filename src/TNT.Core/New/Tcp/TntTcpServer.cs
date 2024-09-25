using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TNT.Core.New.Tcp
{
    public class TntTcpServer : IDisposable
    {
        private IPEndPoint IPEndPoint;
        private TcpListener _tcpListener;

        private volatile int _maxId;

        private ConcurrentDictionary<int, ClientObject> _clients;

        public Channel<TcpData> ResponsesChannel { get; }

        public TntTcpServer(IPEndPoint endPoint)
        {
            IPEndPoint = endPoint;

            _tcpListener = new TcpListener(endPoint);

            _clients = new ConcurrentDictionary<int, ClientObject>();

            ResponsesChannel = Channel.CreateBounded<TcpData>(5);
        }

        public void Start()
        {
            _ = InternalStartAsync();
        }


        public async Task InternalStartAsync()
        {
            _tcpListener.Start();

            while (!_disposed)
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();

                var newId = _maxId++;

                var clientObject = new ClientObject(newId, tcpClient, this);

                _clients.TryAdd(newId, clientObject);

                clientObject.Start();
            }
        }


        public void ClientDisconnected(int id)
        {
            if (_clients.TryRemove(id, out var clientObject))
                clientObject.Dispose();
        }

        private volatile bool _disposed;
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            var clients = _clients.Values;
            foreach (var client in clients)
                client.Dispose();
        }
    }


    public class ClientObject
    {
        private TntTcpClient _tcpClient;
        private TntTcpServer _tntTcpServer;
        private int _id;

        public ClientObject(int newId, TcpClient tcpClient, TntTcpServer tntTcpServer)
        {
            _id = newId;
            _tcpClient = new TntTcpClient(tcpClient);

            _tcpClient.OnDisconnect += TcpClient_OnDisconnect;

            _tntTcpServer = tntTcpServer;
        }


        public void Start()
        {
            _tcpClient.Start();
        }

        private void TcpClient_OnDisconnect(object arg1, Presentation.ErrorMessage arg2)
        {
            Disconnected();
        }

        public void Disconnected()
        {
            _tntTcpServer.ClientDisconnected(_id);
        }

        public void Dispose()
        {
            _tcpClient.Dispose();
        }
    }
}
