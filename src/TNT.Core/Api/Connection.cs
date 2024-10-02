using System;
using TNT.Core.Presentation;
using TNT.Core.Transport;

namespace TNT.Core.Api
{

    public class Connection<TContract> : IDisposable, IConnection<TContract>
    {
        private readonly Action<TContract, IChannel, ErrorMessage> _onContractDisconnected;

        public Connection(TContract contract, IChannel channel, Action<TContract, IChannel, ErrorMessage> onContractDisconnected)
        {
            _onContractDisconnected = onContractDisconnected;
            Contract = contract;
            Channel = channel;
            Channel.OnDisconnect += Channel_OnDisconnect;
        }

        private void Channel_OnDisconnect(object obj, ErrorMessage cause)
        {
            _onContractDisconnected?.Invoke(Contract, Channel, cause);
        }

        public TContract Contract { get; }
        public IChannel Channel { get; }
        public void Dispose()
        {
            if(Channel?.IsConnected == true)
                Channel.Disconnect();
        }
    }
}