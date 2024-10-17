using CommonTestTools.Contracts;
using CommonTestTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Local;

namespace TNT.Core.Tests.Exceprions
{
    [TestFixture]
    public class ConnectionIsLostExceptionTests
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

        [Test]
        public async Task ProxyConnectionIsLost_SayCallThrows()
        {
            _serverAndClient.ClientSideConnection.Dispose();

            await TestTools.AssertThrowsAndNotBlocks<ConnectionIsLostException>(() => _serverAndClient.ClientSideConnection.Contract.Say());
        }
        [Test]
        public async Task ProxyConnectionIsLost_AskCallThrows()
        {
            _serverAndClient.ClientSideConnection.Dispose();

            await TestTools.AssertThrowsAndNotBlocks<ConnectionIsLostException>(() => _serverAndClient.ClientSideConnection.Contract.Ask());
        }
        [Test]
        public async Task ProxyConnectionIsLost_SayAsyncCallThrows()
        {
            _serverAndClient.ClientSideConnection.Dispose();

            await TestTools.AssertThrowsAndNotBlocks<ConnectionIsLostException>(() => _serverAndClient.ClientSideConnection.Contract.SayAsync());
        }
        [Test]
        public async Task ProxyConnectionIsLost_AskAsyncCallThrows()
        {
            _serverAndClient.ClientSideConnection.Dispose();

            await TestTools.AssertThrowsAndNotBlocks<ConnectionIsLostException>(() => _serverAndClient.ClientSideConnection.Contract.AskAsync());
        }



        [Test]
        public async Task ProxyConnectionIsLost_OnSayCallThrows()
        {
            _serverAndClient.ServerSideConnection.Dispose();

            await TestTools.AssertThrowsAndNotBlocks<ConnectionIsLostException>(() => _serverAndClient.ServerSideConnection.Contract.OnSay());
        }
        [Test]
        public async Task ProxyConnectionIsLost_OnAskCallThrows()
        {
            _serverAndClient.ServerSideConnection.Dispose();

            await TestTools.AssertThrowsAndNotBlocks<ConnectionIsLostException>(() => _serverAndClient.ServerSideConnection.Contract.OnAsk());
        }
        [Test]
        public async Task ProxyConnectionIsLost_OnSayAsyncCallThrows()
        {
            _serverAndClient.ServerSideConnection.Dispose();

            await TestTools.AssertThrowsAndNotBlocks<ConnectionIsLostException>(() => _serverAndClient.ServerSideConnection.Contract.FuncTaskWithResult());
        }
        [Test]
        public async Task ProxyConnectionIsLost_OnAskAsyncCallThrows()
        {
            _serverAndClient.ServerSideConnection.Dispose();

            await TestTools.AssertThrowsAndNotBlocks<ConnectionIsLostException>(() => _serverAndClient.ServerSideConnection.Contract.FuncTaskWithResultAndParam(""));
        }
    }
}
