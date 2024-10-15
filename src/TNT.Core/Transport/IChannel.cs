using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using TNT.Core.Presentation;
using TNT.Core.Tcp;

namespace TNT.Core.Transport
{
    public interface IChannel : IDisposable
    {
        /// <summary>
        /// Indicates connection status of downlayer TcpClient
        /// </summary>
        bool IsConnected { get; }

        void Start();
        Task StartAsync();

        public Channel<TcpData> ResponsesChannel { get; }

        /// <summary>
        /// Raising if connection is lost
        /// </summary>
        event Action<object, ErrorMessage> OnDisconnect;
        /// <summary>
        /// Close connection
        /// </summary>
        void Disconnect();

        void DisconnectBecauseOf(ErrorMessage error);
        Task WriteAsync(byte[] data);

        int BytesReceived { get; }
        int BytesSent { get; }

        string RemoteEndpointName { get; }
        string LocalEndpointName { get; }

    }
}
