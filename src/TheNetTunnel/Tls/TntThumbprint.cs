using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TheNetTunnel.Tls
{
    /// <summary>
    /// Certificate thumbprints as TNT understands them: SHA-256 over the DER
    /// encoding, upper-case hex without separators.
    /// </summary>
    public static class TntThumbprint
    {
        /// <summary>SHA-256 thumbprint of <paramref name="certificate"/> in the normalized form.</summary>
        public static string Of(X509Certificate certificate)
        {
            if (certificate == null)
                throw new ArgumentNullException(nameof(certificate));

            return certificate.GetCertHashString(HashAlgorithmName.SHA256).ToUpperInvariant();
        }

        /// <summary>
        /// Strips ":" and spaces and upper-cases <paramref name="thumbprint"/>;
        /// returns null for a null, empty or whitespace input.
        /// </summary>
        public static string Normalize(string thumbprint)
        {
            if (string.IsNullOrWhiteSpace(thumbprint))
                return null;

            return thumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();
        }

        /// <summary>True when both are non-empty and equal after normalization.</summary>
        public static bool AreEqual(string left, string right)
        {
            var l = Normalize(left);
            var r = Normalize(right);

            return l != null && r != null && string.Equals(l, r, StringComparison.Ordinal);
        }
    }
}
