using System;
using System.Security.Cryptography.X509Certificates;

namespace TheNetTunnel.Tls
{
    /// <summary>
    /// TLS settings for the server side of a connection.
    /// </summary>
    public class TntServerTlsOptions
    {
        public TntServerTlsOptions(X509Certificate2 certificate)
        {
            if (certificate == null)
                throw new ArgumentNullException(nameof(certificate));
            if (!certificate.HasPrivateKey)
                throw new ArgumentException("Server certificate must contain a private key", nameof(certificate));

            Certificate = certificate;
        }

        /// <summary>
        /// Certificate (with private key) presented to connecting clients.
        /// </summary>
        public X509Certificate2 Certificate { get; }

        private TimeSpan _handshakeTimeout = TntTlsChannel.DefaultHandshakeTimeout;

        /// <summary>
        /// How long the server waits for an incoming connection to complete the TLS handshake
        /// before dropping it. Should not be shorter than the clients' own
        /// <see cref="TntClientTlsOptions.HandshakeTimeout"/>, otherwise a slow but live client
        /// is dropped by the server while still waiting for an answer.
        /// Defaults to <see cref="TntTlsChannel.DefaultHandshakeTimeout"/>.
        /// </summary>
        public TimeSpan HandshakeTimeout
        {
            get => _handshakeTimeout;
            set => _handshakeTimeout = TntTlsChannel.ValidateHandshakeTimeout(value);
        }
    }
}
