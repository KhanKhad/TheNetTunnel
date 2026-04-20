using System;
using TheNetTunnel.Presentation;
using TheNetTunnel.Transport;

namespace TheNetTunnel.Api
{
    public interface IConnection<out TContract> : IDisposable
    {
        IChannel Channel { get; }
        TContract Contract { get; }
        public IInterlocutor Interlocutor { get; }
    }
}