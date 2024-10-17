using CommonTestTools.Contracts;
using CommonTestTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using TNT.Core.Tcp;
using TNT.Core.Api;
using System.Collections.Concurrent;

namespace TNT.Core.Tests.DispatcherTests
{
    [TestFixture]
    public class SingleOperationDispatcherTests
    {
        private ServerAndClient<ISingleOperationContract, ISingleOperationContract, SingleOperationContract> _serverAndClient;

        [SetUp]
        public async Task TearUp()
        {
            var server = TntBuilder
            .UseContract<ISingleOperationContract, SingleOperationContract>()
            .UseSingleOperationDispatcher()
            .CreateTcpServer(IPAddress.Loopback, 12345);

            server.Start();

            var clientSide = await TntBuilder
               .UseContract<ISingleOperationContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

            var serverSide = await server.WaitForAClient();

            _serverAndClient = new ServerAndClient<ISingleOperationContract, ISingleOperationContract, SingleOperationContract>()
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
        public async Task FewSayMessagesTest()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    _serverAndClient.ClientSideConnection.Contract.Say();
                }));
            }

            await Task.Delay(9000);

            var count = ((SingleOperationContract)_serverAndClient.ServerSideConnection.Contract)._callsCount;

            Assert.That(count, Is.LessThan(10));
        }

        [Test]
        public async Task FewAskMessagesTest()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    _serverAndClient.ClientSideConnection.Contract.Ask();
                }));
            }

            var waitTask = Task.WhenAll(tasks);
            var timeTask = Task.Delay(9000);

            var result = await Task.WhenAny(waitTask, timeTask);

            Assert.That(result == timeTask);
        }

        [Test]
        public async Task FewAsyncSayMessagesTest()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _serverAndClient.ClientSideConnection.Contract.SayAsync();
                }));
            }

            var waitTask = Task.WhenAll(tasks);
            var timeTask = Task.Delay(9000);

            var result = await Task.WhenAny(waitTask, timeTask);

            Assert.That(result == timeTask);
        }

        [Test]
        public async Task FewAsyncAskMessagesTest()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _serverAndClient.ClientSideConnection.Contract.AskAsync();
                }));
            }

            var waitTask = Task.WhenAll(tasks);
            var timeTask = Task.Delay(9000);

            var result = await Task.WhenAny(waitTask, timeTask);

            Assert.That(result == timeTask);
        }


        [Test]
        public async Task AnyMessagesTest()
        {
            var tasks = new List<Task>
            {
                Task.Run(async () =>
                {
                    await _serverAndClient.ClientSideConnection.Contract.AskAsync();
                }),
                Task.Run(() =>
                {
                    _serverAndClient.ClientSideConnection.Contract.Say();
                }),
                Task.Run(async() =>
                {
                    await _serverAndClient.ClientSideConnection.Contract.SayAsync();
                }),
                Task.Run(() =>
                {
                    _serverAndClient.ClientSideConnection.Contract.Ask();
                }),
                Task.Run(() =>
                {
                    _serverAndClient.ClientSideConnection.Contract.Say();
                }),
                Task.Run(async () =>
                {
                    await _serverAndClient.ClientSideConnection.Contract.AskAsync();
                }),
                Task.Run(() =>
                {
                    _serverAndClient.ClientSideConnection.Contract.Ask();
                }),
                Task.Run(async() =>
                {
                    await _serverAndClient.ClientSideConnection.Contract.SayAsync();
                }),
            };

            await Task.Delay(7000);

            var count = ((SingleOperationContract)_serverAndClient.ServerSideConnection.Contract)._callsCount;

            Assert.That(count, Is.LessThan(8));
        }
    }
}
