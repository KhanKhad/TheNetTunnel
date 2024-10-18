using CommonTestTools.Contracts;
using CommonTestTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNT.Core.Tests.SyncTests
{
    [TestFixture]
    public class SyncCallsTests
    {
        private ServerAndClient<ITestContract, ITestContract, TestContractMock> _serverAndClient;
        private TestContractMock _serverSideContractImpl;
        [SetUp]
        public async Task SetUp()
        {
            _serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.Create();
            _serverSideContractImpl = (TestContractMock)_serverAndClient.ServerSideConnection.Contract;
        }

        [TearDown]
        public void Disposing()
        {
            _serverAndClient.Dispose();
        }

        [Test]
        public async Task SayNoParamsTest()
        {
            await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.Say());
            
            await Task.Delay(300);

            var received = _serverSideContractImpl.SayCalledCount;

            Assert.That(received == 1);
        }

        [TestCase("Hey you")]
        [TestCase("")]
        [TestCase(null)]
        public async Task SayWithParamsTest(string sentMessage)
        {
            await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.Say(sentMessage));

            await Task.Delay(300);

            var received = _serverSideContractImpl.SaySCalled.Single();

            Assert.That(sentMessage == received);
        }


        [Test]
        public async Task AskNoParamsTest()
        {
            var res = await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.Ask());

            Assert.That(res == TestContractMock.AskReturns);
        }

        [TestCase("Hey you", 12, 24)]
        [TestCase("", 234, 0)]
        [TestCase(null, 0, long.MaxValue)]
        public void AskWithParamsTest(string s, int i, long l)
        {
            var func = new Func<string, int, long, string>((s1, i2, l3) =>
            {
                return s1 + i2.ToString() + l3.ToString();
            });

            _serverSideContractImpl.WhenAskSILCalledCall(func);

            var proxyResult = _serverAndClient.ClientSideConnection.Contract.Ask(s, i, l);
            var originResult = func(s, i, l);

            Assert.That(originResult == proxyResult);
        }
    }

}
