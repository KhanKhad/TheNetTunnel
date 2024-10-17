using CommonTestTools.Contracts;
using CommonTestTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace TNT.Core.Tests.SyncTests
{
    [TestFixture]
    public class FewMessagesAtTheSameTimeTests
    {
        private ServerAndClient<ITestContract, ITestContract, TestContractMock> _serverAndClient;

        [SetUp]
        public async Task TearUp()
        {
            _serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.Create();
        }

        [TearDown]
        public void Disposing()
        {
            _serverAndClient.Dispose();
        }

        [TestCase("msg")]
        [TestCase(null)]
        [TestCase("null")]
        public async Task FewMessagesTest(string msg)
        {
            var tasks = new List<Task>();

            var bag = new ConcurrentBag<string>();

            //pool exhaust, dont set i more than 15
            for (int i = 0; i < 10; i++) 
            {
                tasks.Add(Task.Run(() => 
                {
                    var res = _serverAndClient.ClientSideConnection.Contract.Ask(msg);
                    bag.Add(res);
                }));
            }

            await Task.WhenAll(tasks);
            
            Assert.That(bag.Count == 10);

            foreach (var item in bag)
            {
                Assert.That(item== msg);
            }
        }
        [Test]
        public async Task NewMessageInEventTest()
        {
            var val = 0;
            _serverAndClient.ClientSideConnection.Contract.OnAsk += () => val = _serverAndClient.ClientSideConnection.Contract.Ask();

            var rRes = await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.OnAsk());
            Assert.That(TestContractMock.AskReturns == rRes);
            Assert.That(TestContractMock.AskReturns == val);
        }
    }
}
