using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using CommonTestTools;
using CommonTestTools.Contracts;
using NUnit.Framework;
using TheNetTunnel.Api;
using TheNetTunnel.Tcp;

namespace TheNetTunnel.Tests.Tcp
{
    [TestFixture]
    public class ConnectionLifecycleTests
    {
        private const int DetectionTimeoutMs = 3000;

        [Test]
        public async Task ServerDisposed_ClientDetectsDisconnect()
        {
            ServerAndClient<ITestContract, ITestContract, TestContractMock> serverAndClient = null;
            try
            {
                serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.CreateAsync(12413);

                var clientChannelDisconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                serverAndClient.ClientSideConnection.Channel.OnDisconnect += (_, _) => clientChannelDisconnected.TrySetResult();

                serverAndClient.TntTcpServer.Dispose();

                var completed = await Task.WhenAny(clientChannelDisconnected.Task, Task.Delay(DetectionTimeoutMs));
                Assert.That(completed, Is.EqualTo(clientChannelDisconnected.Task),
                    "Client did not detect that the server went away");
            }
            finally
            {
                serverAndClient?.Dispose();
            }
        }

        [Test]
        public async Task ClientDisconnected_ServerConnectionsCountDropsToZero()
        {
            ServerAndClient<ITestContract, ITestContract, TestContractMock> serverAndClient = null;
            try
            {
                serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.CreateAsync(12414);
                Assert.That(serverAndClient.TntTcpServer.ConnectionsCount, Is.EqualTo(1));

                serverAndClient.ClientSideConnection.Dispose();

                // The server cleans the connection up asynchronously.
                var sw = Stopwatch.StartNew();
                while (serverAndClient.TntTcpServer.ConnectionsCount > 0 && sw.ElapsedMilliseconds < DetectionTimeoutMs)
                    await Task.Delay(20);

                Assert.That(serverAndClient.TntTcpServer.ConnectionsCount, Is.Zero,
                    "Server must remove the dead connection");
            }
            finally
            {
                serverAndClient?.Dispose();
            }
        }

        [Test]
        public async Task ConnectionDisposedTwice_DoesNotThrow()
        {
            var serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.CreateAsync(12415);
            try
            {
                Assert.DoesNotThrow(() =>
                {
                    serverAndClient.ClientSideConnection.Dispose();
                    serverAndClient.ClientSideConnection.Dispose();
                });
            }
            finally
            {
                serverAndClient.Dispose();
            }
        }

        [Test]
        public async Task ServerDisposedTwice_DoesNotThrow()
        {
            var serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.CreateAsync(12416);
            try
            {
                Assert.DoesNotThrow(() =>
                {
                    serverAndClient.TntTcpServer.Dispose();
                    serverAndClient.TntTcpServer.Dispose();
                });
            }
            finally
            {
                serverAndClient.Dispose();
            }
        }

        [Test]
        public async Task NewClientCanConnect_AfterPreviousOneDisconnected()
        {
            ServerAndClient<ITestContract, ITestContract, TestContractMock> serverAndClient = null;
            try
            {
                serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.CreateAsync(12417);

                serverAndClient.ClientSideConnection.Dispose();

                var serverSideTask = serverAndClient.TntTcpServer.WaitForAClient();

                var secondClient = await TntBuilder
                    .UseContract<ITestContract>()
                    .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12417);

                await serverSideTask;

                Assert.That(secondClient.Contract.Ask(), Is.EqualTo(TestContractMock.AskReturns));

                secondClient.Dispose();
            }
            finally
            {
                serverAndClient?.Dispose();
            }
        }
    }
}
