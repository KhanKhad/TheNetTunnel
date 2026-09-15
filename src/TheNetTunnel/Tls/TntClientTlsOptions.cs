namespace TheNetTunnel.Tls
{
    /// <summary>
    /// TLS settings for the client side of a connection.
    /// </summary>
    public class TntClientTlsOptions
    {
        /// <summary>
        /// SHA-1 thumbprint (hex, case-insensitive) of the certificate the server
        /// is expected to present. When set, the server is accepted only if its
        /// certificate thumbprint matches, regardless of chain trust — this is
        /// how self-signed certificates are used. When null, the certificate is
        /// validated by the standard OS chain rules.
        /// </summary>
        public string ExpectedServerThumbprint { get; set; }

        /// <summary>
        /// Host name sent in SNI and matched against the server certificate
        /// during standard validation. Defaults to the endpoint IP address.
        /// </summary>
        public string TargetHost { get; set; }
    }
}
