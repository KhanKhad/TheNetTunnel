using CommonTestTools;
using CommonTestTools.Contracts;
using NUnit.Framework;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TheNetTunnel.Api;
using TheNetTunnel.Tcp;

namespace TheNetTunnel.Tests.DispatcherTests
{
    [TestFixture]
    public class OrderedReceiveTests
    {
        private ServerAndClient<IOrderedReceiveContract, IOrderedReceiveContract, OrderedReceiveContract> _serverAndClient;

        [SetUp]
        public async Task SetUp()
        {
            var server = TntBuilder
            .UseContract<IOrderedReceiveContract, OrderedReceiveContract>()
            .CreateTcpServer(IPAddress.Loopback, 12345);

            server.Start();

            var clientTask = server.WaitForAClient();

            var clientSide = await TntBuilder
               .UseContract<IOrderedReceiveContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

            var serverSide = await clientTask;

            _serverAndClient = new ServerAndClient<IOrderedReceiveContract, IOrderedReceiveContract, OrderedReceiveContract>()
            {
                ClientSideConnection = clientSide,
                ServerSideConnection = serverSide,
                TntTcpServer = server,
            };
        }

        [TearDown]
        public void Disposing()
        {
            _serverAndClient.Dispose();
        }

        [Test]
        public async Task SayMessages_AreHandledInSendOrder()
        {
            const int messagesCount = 2000;

            for (int i = 0; i < messagesCount; i++)
                _serverAndClient.ClientSideConnection.Contract.Say(i);

            var contract = (OrderedReceiveContract)_serverAndClient.ServerSideConnection.Contract;

            var sw = Stopwatch.StartNew();

            while (contract.ReceivedValues.Count < messagesCount && sw.ElapsedMilliseconds < 15000)
                await Task.Delay(50);

            Assert.That(contract.ReceivedValues.Count, Is.EqualTo(messagesCount));
            Assert.That(contract.ReceivedValues.ToArray(), Is.EqualTo(Enumerable.Range(0, messagesCount).ToArray()));
        }
    }
}
