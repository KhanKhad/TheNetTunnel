using System;

namespace TheNetTunnel.Exceptions.Local
{
    /// <summary>
    /// TLS handshake failed. <see cref="RemoteThumbprint"/> carries the SHA-256
    /// thumbprint of the certificate the remote side presented (if it got that far),
    /// so a client can show/pin it even when validation rejected it.
    /// </summary>
    public class SslAuthenticateException : Exception
    {
        public string RemoteThumbprint { get; }

        public SslAuthenticateException(string message, string remoteThumbprint, Exception innerException)
            : base(message, innerException)
        {
            RemoteThumbprint = remoteThumbprint;
        }
    }
}
