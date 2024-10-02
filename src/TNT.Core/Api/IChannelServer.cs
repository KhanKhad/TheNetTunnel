using System;
using System.Collections.Generic;
using TNT.Core.Presentation;
using TNT.Core.Transport;

namespace TNT.Core.Api
{
   public interface  IChannelServer<TContract> : IDisposable 
    {
        event Action<object, BeforeConnectEventArgs<TContract>>  BeforeConnect;
        event Action<object, IConnection<TContract>> AfterConnect;
        event Action<object, ClientDisconnectEventArgs<TContract>> Disconnected;
        int ConnectionsCount { get; }
        void Start();
        IEnumerable<IConnection<TContract>> GetAllConnections();
    }

    public class ClientDisconnectEventArgs<TContract>: EventArgs
    {
        public ClientDisconnectEventArgs(IConnection<TContract> connection, ErrorMessage errorMessageOrNull)
        {
            Connection = connection;
            ErrorMessageOrNull = errorMessageOrNull;
        }

        public IConnection<TContract> Connection { get; }
        public ErrorMessage ErrorMessageOrNull { get; }
    }
    public class BeforeConnectEventArgs<TContract> : EventArgs
    {
        public BeforeConnectEventArgs(IConnection<TContract> connection)
        {
            Connection = connection;
            AllowConnection = true;
        }

        public IConnection<TContract> Connection { get; }
        public bool AllowConnection { get; set; }
    }
}
