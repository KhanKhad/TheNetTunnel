using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CommonTestTools;
using CommonTestTools.Contracts;
using NUnit.Framework;
using TheNetTunnel.Api;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Tcp;
using TheNetTunnel.Tls;

namespace TheNetTunnel.Tests.Tls
{
    [TestFixture]
    public class TlsTests
    {
        private const int ConnectTimeoutMs = 5000;
        // Mirrors the server-side prepare/handshake timeout.
        private const int HandshakeTimeoutMs = 10000;

        private static X509Certificate2 _certificate;

        [OneTimeSetUp]
        public void CreateCertificate()
        {
            _certificate = TestCertificates.CreateSelfSigned();
        }

        [OneTimeTearDown]
        public void DisposeCertificate()
        {
            _certificate.Dispose();
        }

        private static TntServerTlsOptions ServerTls() => new TntServerTlsOptions(_certificate);

        private static TntClientTlsOptions PinnedClientTls() => new TntClientTlsOptions
        {
            ExpectedServerThumbprints = new[] { TntThumbprint.Of(_certificate) },
        };

        [Test]
        public async Task Roundtrip_WithPinnedThumbprint()
        {
            using var serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>
                .CreateAsync(12501, ServerTls(), PinnedClientTls());

            var answer = await serverAndClient.ClientSideConnection.Contract.AskAsync("hello over tls");
            Assert.That(answer, Is.EqualTo("hello over tls"));

            var clientChannel = serverAndClient.ClientSideConnection.Channel as TntTlsChannel;
            Assert.That(clientChannel, Is.Not.Null, "Client channel must be a TLS channel");
            Assert.That(clientChannel.IsConnected, Is.True);
            Assert.That(clientChannel.RemoteCertificate, Is.Not.Null);
            Assert.That(clientChannel.RemoteCertificate.Thumbprint, Is.EqualTo(_certificate.Thumbprint));
            Assert.That(clientChannel.RemoteThumbprint, Is.EqualTo(TntThumbprint.Of(_certificate)));

            var serverChannel = serverAndClient.ServerSideConnection.Channel as TntTlsChannel;
            Assert.That(serverChannel, Is.Not.Null, "Server channel must be a TLS channel");
            Assert.That(serverChannel.IsConnected, Is.True);
        }

        [Test]
        public async Task LargeMessage_OverTls()
        {
            using var serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>
                .CreateAsync(12502, ServerTls(), PinnedClientTls());

            // Well above one TLS record (16 KB) and above the receive chunk size.
            var payload = new string('x', 300 * 1024);

            var answer = await serverAndClient.ClientSideConnection.Contract.AskAsync(payload);
            Assert.That(answer, Is.EqualTo(payload));
        }

        [Test]
        public async Task WrongThumbprint_ClientFails_ServerStillAccepts()
        {
            const int port = 12503;

            using var server = TntBuilder
                .UseContract<ITestContract, TestContractMock>()
                .UseTls(ServerTls())
                .CreateTcpServer(IPAddress.Loopback, port);
            server.Start();

            var wrongPin = new TntClientTlsOptions
            {
                ExpectedServerThumbprints = new[] { new string('0', 64) },
            };

            Assert.ThrowsAsync<SslAuthenticateException>(() => TntBuilder
                .UseContract<ITestContract>()
                .UseTls(wrongPin)
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, port));

            var waitForAClient = server.WaitForAClient();

            using var goodClient = await TntBuilder
                .UseContract<ITestContract>()
                .UseTls(PinnedClientTls())
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, port);

            var accepted = await Task.WhenAny(waitForAClient, Task.Delay(ConnectTimeoutMs));
            Assert.That(accepted, Is.EqualTo(waitForAClient), "Server must keep accepting after a failed handshake");
            Assert.That(server.ConnectionsCount, Is.EqualTo(1));
        }

        [Test]
        public async Task OneOfSeveralPins_IsEnough()
        {
            var pins = new TntClientTlsOptions
            {
                ExpectedServerThumbprints = new[]
                {
                    new string('0', 64),
                    TntThumbprint.Of(_certificate).ToLowerInvariant(),
                    "ff:ff",
                },
            };

            using var serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>
                .CreateAsync(12507, ServerTls(), pins);

            var answer = await serverAndClient.ClientSideConnection.Contract.AskAsync("pinned");
            Assert.That(answer, Is.EqualTo("pinned"));
        }

        [Test]
        public void Sha1Thumbprint_IsNotAccepted()
        {
            const int port = 12508;

            using var server = TntBuilder
                .UseContract<ITestContract, TestContractMock>()
                .UseTls(ServerTls())
                .CreateTcpServer(IPAddress.Loopback, port);
            server.Start();

            // X509Certificate2.Thumbprint is SHA-1; pins are SHA-256 only.
            var sha1Pin = new TntClientTlsOptions
            {
                ExpectedServerThumbprints = new[] { _certificate.Thumbprint },
            };

            Assert.ThrowsAsync<SslAuthenticateException>(() => TntBuilder
                .UseContract<ITestContract>()
                .UseTls(sha1Pin)
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, port));
        }

        [Test]
        public async Task EmptyPins_AcceptAnyCertificate()
        {
            // Blank entries normalize to nothing, which is the same as no pins at all.
            var blankPins = new TntClientTlsOptions { ExpectedServerThumbprints = new[] { "", " " } };

            using var serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>
                .CreateAsync(12510, ServerTls(), blankPins);

            var answer = await serverAndClient.ClientSideConnection.Contract.AskAsync("no pins");
            Assert.That(answer, Is.EqualTo("no pins"));
        }

        [Test]
        public void Thumbprint_NormalizesAndCompares()
        {
            Assert.That(TntThumbprint.Normalize("ab:12 cd"), Is.EqualTo("AB12CD"));
            Assert.That(TntThumbprint.Normalize("  "), Is.Null);
            Assert.That(TntThumbprint.AreEqual("AB:12", "ab12"), Is.True);
            Assert.That(TntThumbprint.AreEqual("", ""), Is.False);
            Assert.That(TntThumbprint.Of(_certificate), Has.Length.EqualTo(64));
        }

        [Test]
        public async Task SelfSignedWithoutPin_IsAccepted()
        {
            // No pins: the self-signed certificate is accepted without any validation.
            using var serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>
                .CreateAsync(12504, ServerTls(), new TntClientTlsOptions());

            var answer = await serverAndClient.ClientSideConnection.Contract.AskAsync("self-signed");
            Assert.That(answer, Is.EqualTo("self-signed"));

            var clientChannel = serverAndClient.ClientSideConnection.Channel as TntTlsChannel;
            Assert.That(clientChannel, Is.Not.Null);
            Assert.That(clientChannel.RemoteThumbprint, Is.EqualTo(TntThumbprint.Of(_certificate)));
        }

        [Test]
        public async Task SilentClient_IsDroppedByTimeout_ServerAcceptsNext()
        {
            const int port = 12505;

            using var server = TntBuilder
                .UseContract<ITestContract, TestContractMock>()
                .UseTls(ServerTls())
                .CreateTcpServer(IPAddress.Loopback, port);
            server.Start();

            // Connects at the TCP level and never starts the TLS handshake.
            using var silent = new TcpClient();
            await silent.ConnectAsync(IPAddress.Loopback, port);

            // The server must close the socket once the handshake timeout expires:
            // a receive on our side then completes with 0 bytes or a reset.
            var dropped = Task.Run(async () =>
            {
                try
                {
                    var read = await silent.Client.ReceiveAsync(new byte[1], SocketFlags.None);
                    return read == 0;
                }
                catch (SocketException)
                {
                    return true;
                }
            });

            var completed = await Task.WhenAny(dropped, Task.Delay(HandshakeTimeoutMs + ConnectTimeoutMs));
            Assert.That(completed, Is.EqualTo(dropped), "Server did not drop the silent client");
            Assert.That(await dropped, Is.True);

            var waitForAClient = server.WaitForAClient();

            using var goodClient = await TntBuilder
                .UseContract<ITestContract>()
                .UseTls(PinnedClientTls())
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, port);

            var accepted = await Task.WhenAny(waitForAClient, Task.Delay(ConnectTimeoutMs));
            Assert.That(accepted, Is.EqualTo(waitForAClient), "Server must keep accepting after dropping a silent client");

            var answer = await goodClient.Contract.AskAsync("still alive");
            Assert.That(answer, Is.EqualTo("still alive"));
        }

        [Test]
        public async Task WithoutUseTls_PlainTcpChannelIsUsed()
        {
            using var serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>
                .CreateAsync(12506);

            Assert.That(serverAndClient.ClientSideConnection.Channel, Is.TypeOf<TntTcpClient>());
            Assert.That(serverAndClient.ServerSideConnection.Channel, Is.TypeOf<TntTcpClient>());
        }

        [Test]
        public void ServerOptionsOnClientBuilder_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                TntBuilder.UseContract<ITestContract>().UseTls(ServerTls()));
        }

        [Test]
        public void ClientOptionsOnServerBuilder_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                TntBuilder.UseContract<ITestContract, TestContractMock>().UseTls(PinnedClientTls()));
        }

        [Test]
        public void ServerOptions_RequireCertificateWithPrivateKey()
        {
            using var publicOnly = new X509Certificate2(_certificate.Export(X509ContentType.Cert));

            Assert.Throws<ArgumentException>(() => new TntServerTlsOptions(publicOnly));
            Assert.Throws<ArgumentNullException>(() => new TntServerTlsOptions(null));
        }
    }
}
