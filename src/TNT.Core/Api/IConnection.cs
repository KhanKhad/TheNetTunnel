using System;
using TNT.Core.Transport;

namespace TNT.Core.Api
{
    public interface IConnection<out TContract, out TChannel> : IDisposable
        where TChannel : IChannel
    {
        TChannel Channel { get; }
        TContract Contract { get; }
    }
}