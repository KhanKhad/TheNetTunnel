using System;

namespace TheNetTunnel.Tls
{
    /// <summary>
    /// TLS settings for the client side of a connection.
    /// </summary>
    public class TntClientTlsOptions
    {
        /// <summary>
        /// SHA-256 thumbprints (hex, case-insensitive, ":" and spaces ignored) of the
        /// certificates the server is allowed to present. When set and not empty, the
        /// server is accepted only if its certificate thumbprint matches one of them,
        /// regardless of chain trust — this is how self-signed certificates are used
        /// and how a certificate rotation is survived (pin both the old and the new one).
        /// When null or empty, the server certificate is not validated at all: any
        /// certificate is accepted, so the connection is encrypted but the server is
        /// not authenticated. Compute a thumbprint with <see cref="TntThumbprint.Of"/>.
        /// </summary>
        public string[] ExpectedServerThumbprints { get; set; }

        /// <summary>
        /// Host name sent in SNI. Defaults to the endpoint IP address.
        /// </summary>
        public string TargetHost { get; set; }

        private TimeSpan _handshakeTimeout = TntTlsChannel.DefaultHandshakeTimeout;

        /// <summary>
        /// How long the client waits for the TLS handshake to complete, counted from the
        /// moment the TCP connection is established. A slow server (a weak controller right
        /// after start, or one busy with other handshakes) needs a longer value.
        /// Defaults to <see cref="TntTlsChannel.DefaultHandshakeTimeout"/>.
        /// </summary>
        public TimeSpan HandshakeTimeout
        {
            get => _handshakeTimeout;
            set => _handshakeTimeout = TntTlsChannel.ValidateHandshakeTimeout(value);
        }
    }
}
