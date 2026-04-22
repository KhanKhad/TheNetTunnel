using System;
using System.Threading;
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
        
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (Contract is IDisposable disposableContract)
                disposableContract.Dispose();

            Interlocutor.Dispose();
            Channel.Dispose();
        }
    }
}