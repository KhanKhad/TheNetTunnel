using System;
using TNT.Core.Presentation;
using TNT.Core.Transport;

namespace TNT.Core.Api
{

    public class Connection<TContract> : IDisposable, IConnection<TContract>
    {
        public Connection(TContract contract, IChannel channel)
        {
            Contract = contract;
            Channel = channel;
        }

        public TContract Contract { get; }
        public IChannel Channel { get; }

        public void Dispose()
        {
            Channel.Dispose();
        }
    }
}