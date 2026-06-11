using CommonTestTools;
using CommonTestTools.Contracts;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
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

        [Test]
        public async Task Events_AreHandledInSendOrder()
        {
            const int eventsCount = 2000;
            var receivedValues = new ConcurrentQueue<int>();

            _serverAndClient.ClientSideConnection.Contract.Event += abc;

            void abc(int value)
            {
                //jitter makes reordering of concurrently handled messages very likely
                if (value % 5 == 0)
                    Thread.Sleep(1);

                receivedValues.Enqueue(value);
            }

            for (int i = 0; i < eventsCount; i++)
                _serverAndClient.ServerSideConnection.Contract.Event(i);

            var sw = Stopwatch.StartNew();

            while (receivedValues.Count < eventsCount && sw.ElapsedMilliseconds < 15000)
                await Task.Delay(50);

            Assert.That(receivedValues.Count, Is.EqualTo(eventsCount));
            Assert.That(receivedValues.ToArray(), Is.EqualTo(Enumerable.Range(0, eventsCount).ToArray()));
        }


    }
}
