using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CommonTestTools
{
    public static class TestCertificates
    {
        /// <summary>
        /// Self-signed certificate for localhost / 127.0.0.1 with a private key.
        /// </summary>
        public static X509Certificate2 CreateSelfSigned(string commonName = "localhost")
        {
            using var rsa = RSA.Create(2048);

            var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // serverAuth

            var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            using var ephemeral = request.CreateSelfSigned(notBefore, notBefore.AddDays(1));

            // On Windows SChannel cannot use the ephemeral key produced by
            // CreateSelfSigned; a PKCS#12 round-trip persists it in a usable form.
            return new X509Certificate2(ephemeral.Export(X509ContentType.Pkcs12), (string)null,
                X509KeyStorageFlags.Exportable);
        }
    }
}
