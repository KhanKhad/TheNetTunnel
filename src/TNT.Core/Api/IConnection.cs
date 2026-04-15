using System;
using TNT.Core.Presentation;
using TNT.Core.Transport;

namespace TNT.Core.Api
{
    public interface IConnection<out TContract> : IDisposable
    {
        IChannel Channel { get; }
        TContract Contract { get; }
        public IInterlocutor Interlocutor { get; }
    }
}