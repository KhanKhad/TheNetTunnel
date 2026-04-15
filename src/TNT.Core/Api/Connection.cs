using System;
using TNT.Core.Presentation;
using TNT.Core.Transport;

namespace TNT.Core.Api
{

    public class Connection<TContract> : IDisposable, IConnection<TContract>
    {
        public Connection(TContract contract, IChannel channel, IInterlocutor interlocutor)
        {
            Contract = contract;
            Channel = channel;
            Interlocutor = interlocutor;
        }

        public TContract Contract { get; }
        public IChannel Channel { get; }
        public IInterlocutor Interlocutor { get; }

        public void Dispose()
        {
            Channel.Dispose();
        }
    }
}