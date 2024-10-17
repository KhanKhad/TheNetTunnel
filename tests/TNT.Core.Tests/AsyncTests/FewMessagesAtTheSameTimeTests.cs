using CommonTestTools.Contracts;
using CommonTestTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace TNT.Core.Tests.AsyncTests
{
    [TestFixture]
    public class FewMessagesAtTheSameTimeTests
    {
        private ServerAndClient<ITestContract, ITestContract, TestContractMock> _serverAndClient;
        private TestContractMock _serverSideContractImpl;
        [SetUp]
        public async Task TearUp()
        {
            _serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.Create();
            _serverSideContractImpl = (TestContractMock)_serverAndClient.ServerSideConnection.Contract;
        }

        [TearDown]
        public void Disposing()
        {
            _serverAndClient.Dispose();
        }

        [TestCase("msg")]
        [TestCase(null)]
        [TestCase("null")]
        public async Task FewSayMessagesTest(string msg)
        {
            var tasks = new List<Task>();

            var bag = _serverSideContractImpl.SaySCalled;

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _serverAndClient.ClientSideConnection.Contract.SayAsync(msg);
                }));
            }

            await Task.WhenAll(tasks);

            Assert.That(bag.Count == 100);

            foreach (var item in bag)
            {
                Assert.That(item == msg);
            }
        }

        [TestCase("msg")]
        [TestCase(null)]
        [TestCase("null")]
        public async Task FewAskMessagesTest(string msg)
        {
            var tasks = new List<Task>();

            var bag = new ConcurrentBag<string>();

            for (int i = 0; i < 100; i++) 
            {
                tasks.Add(Task.Run(async () => 
                {
                    var res = await _serverAndClient.ClientSideConnection.Contract.AskAsync(msg);
                    bag.Add(res);
                }));
            }

            await Task.WhenAll(tasks);
            
            Assert.That(bag.Count == 100);

            foreach (var item in bag)
            {
                Assert.That(item== msg);
            }
        }
        [Test]
        public async Task NewMessageInEventTest()
        {
            var val = 0;
            _serverAndClient.ClientSideConnection.Contract.FuncTaskWithResult += async () => val = await _serverAndClient.ClientSideConnection.Contract.AskAsync();

            var rRes = await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.FuncTaskWithResult());
            Assert.That(TestContractMock.AskReturns == rRes);
            Assert.That(TestContractMock.AskReturns == val);
        }
    }
}
