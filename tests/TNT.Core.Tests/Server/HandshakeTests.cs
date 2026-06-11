using System;
using System.Net;
using System.Threading.Tasks;
using CommonTestTools.Contracts;
using NUnit.Framework;
using TheNetTunnel.Api;
using TheNetTunnel.Contract;
using TheNetTunnel.Tcp;

namespace TheNetTunnel.Tests.Server
{
    [TntMinimalClientVersion(2, 0, 0)]
    public interface IVersionRestrictedContract
    {
        [TntMessage(1)] void Poke();
    }

    public class VersionRestrictedContract : IVersionRestrictedContract
    {
        public void Poke() { }
    }

    /// <summary>
    /// The rejection reason must actually reach the client before the server
    /// drops the connection (the response is flushed through the send queue).
    /// </summary>
    [TestFixture]
    public class HandshakeTests
    {
        [Test]
        public void ClientVersionBelowMinimal_ClientReceivesRejectionReason()
        {
            TntTcpServer<IVersionRestrictedContract> server = null;
            try
            {
                // The interface demands client >= 2.0.0 while the default client version is 1.0.0.
                server = TntBuilder
                    .UseContract<IVersionRestrictedContract, VersionRestrictedContract>()
                    .CreateTcpServer(IPAddress.Loopback, 12410);
                server.Start();

                var ex = Assert.ThrowsAsync<Exception>(async () =>
                    await TntBuilder
                        .UseContract<IVersionRestrictedContract>()
                        .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12410));

                Assert.That(ex.Message, Does.Contain("Version is not supported"),
                    "The client must receive the reason, not just a dropped socket");
            }
            finally
            {
                server?.Dispose();
            }
        }

        [Test]
        public async Task ConnectionsLimitReached_SecondClientReceivesReason_FirstKeepsWorking()
        {
            TntTcpServer<ITestContract> server = null;
            IConnection<ITestContract> firstClient = null;
            try
            {
                server = TntBuilder
                    .UseContract<ITestContract, TestContractMock>()
                    .CreateTcpServer(IPAddress.Loopback, 12411, maxConnections: 1);
                server.Start();

                var serverSideTask = server.WaitForAClient();

                firstClient = await TntBuilder
                    .UseContract<ITestContract>()
                    .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12411);

                await serverSideTask;

                var ex = Assert.ThrowsAsync<Exception>(async () =>
                    await TntBuilder
                        .UseContract<ITestContract>()
                        .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12411));

                Assert.That(ex.Message, Does.Contain("Connections limit reached"));

                // The rejected handshake must not affect the accepted client.
                Assert.That(firstClient.Contract.Ask(), Is.EqualTo(TestContractMock.AskReturns));
            }
            finally
            {
                firstClient?.Dispose();
                server?.Dispose();
            }
        }
    }
}
