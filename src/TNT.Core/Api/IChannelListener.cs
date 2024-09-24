using System;
using TNT.Core.Transport;

namespace TNT.Core.Api
{
    public interface IChannelListener<out TChannel> where TChannel : IChannel
    {
        bool IsListening { get; set; }
        event Action<IChannelListener<TChannel>, TChannel> Accepted;
    }
}