using System;
using TheNetTunnel.Presentation;
using TheNetTunnel.Transport;

namespace TheNetTunnel.Api
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
            Interlocutor.Dispose();
            Channel.Dispose();
        }
    }
}