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
    }
}
