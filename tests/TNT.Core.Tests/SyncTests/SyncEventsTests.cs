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
    public class SyncEventsTests
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

        [Test]
        public async Task SayNoParamsTest()
        {
            var res = false;
            _serverAndClient.ClientSideConnection.Contract.OnSay += () => res = true;
            await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.OnSay());

            await Task.Delay(300);

            Assert.That(res);
        }

        [TestCase("asd")]
        [TestCase(null)]
        public async Task SayWithParamsTest(string msg)
        {
            var res = string.Empty;

            _serverAndClient.ClientSideConnection.Contract.OnSayS += (a) => res = a;
            await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.OnSayS(msg));

            await Task.Delay(300);

            Assert.That(res == msg);
        }

        [TestCase(0)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void ProxyAskCallNoParams_ReturnsSettedValue(int returnedValue)
        {
            _serverAndClient.ClientSideConnection.Contract.OnAsk += () => returnedValue;
            var proxyResult = _serverAndClient.ServerSideConnection.Contract.OnAsk();

            Assert.That(returnedValue == proxyResult);
        }

        [TestCase("Hey you")]
        [TestCase("")]
        [TestCase(null)]
        public void ProxyAskCall_ReturnsSettedValue(string returnedValue)
        {
            //set 'echo' handler
            _serverAndClient.ClientSideConnection.Contract.OnAskS += (arg) => arg;
            //call
            var proxyResult = _serverAndClient.ServerSideConnection.Contract.OnAskS(returnedValue);
            Assert.That(returnedValue == proxyResult);
        }
    }
}
