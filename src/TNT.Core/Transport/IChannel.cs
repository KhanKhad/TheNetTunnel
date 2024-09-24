﻿using System;
using System.Threading.Tasks;
using TNT.Core.Presentation;

namespace TNT.Core.Transport
{
    public interface IChannel
    {
        /// <summary>
        /// Indicates connection status of downlayer TcpClient
        /// </summary>
        bool IsConnected { get; }
        /// <summary>
        /// Can Channel handle messages now?.
        /// </summary>
        bool AllowReceive { get; set; }
        /// <summary>
        /// Raising on new channel message received.
        /// It is blocking operation (ICHannel cannot handle other messages, while OnReceive handling)
        /// </summary>
        event Action<object, byte[]> OnReceive;
        /// <summary>
        /// Raising if connection is lost
        /// </summary>
        event Action<object, ErrorMessage> OnDisconnect;
        /// <summary>
        /// Close connection
        /// </summary>
        void Disconnect();

        void DisconnectBecauseOf(ErrorMessage error);
        Task WriteAsync(byte[] data, int offset, int length);
        void Write(byte[] array, int offset, int length);

        int BytesReceived { get; }
        int BytesSent { get; }

        string RemoteEndpointName { get; }
        string LocalEndpointName { get; }

    }
}
