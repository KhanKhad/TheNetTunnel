using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TNT.Core.Presentation;
using TNT.Core.Tcp;
using TNT.Core.Transport;

namespace CommonTestTools
{
    public class ChannelMock : IChannel
    {
        public bool IsConnected => true;

        public Channel<TcpData> ResponsesChannel { get; } = Channel.CreateUnbounded<TcpData>();

        public int BytesReceived => 0;

        public int BytesSent => 0;

        public string RemoteEndpointName => "";

        public string LocalEndpointName => "";

        public event Action<object, ErrorMessage> OnDisconnect;

        public void Disconnect()
        {

        }

        public void DisconnectBecauseOf(ErrorMessage error)
        {

        }

        public void Dispose()
        {

        }

        public void Start()
        {

        }

        public Task StartAsync()
        {
            return Task.CompletedTask;
        }

        public Task WriteAsync(byte[] data)
        {
            return Task.CompletedTask;
        }
    }
}
