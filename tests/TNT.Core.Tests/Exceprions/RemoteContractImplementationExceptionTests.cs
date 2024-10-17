using CommonTestTools.Contracts;
using CommonTestTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Remote;

namespace TNT.Core.Tests.Exceprions
{
    [TestFixture]
    public class RemoteContractImplementationExceptionTests
    {
        private ServerAndClient<ITestContract, IEmptyContract, EmptyContract> _emptyServer;
        private ServerAndClient<IEmptyContract, ITestContract, TestContractMock> _emptyClient;

        [SetUp]
        public async Task TearUp()
        {
            _emptyServer = await ServerAndClient<ITestContract, IEmptyContract, EmptyContract>.Create();
            _emptyClient = await ServerAndClient<IEmptyContract, ITestContract, TestContractMock>.Create(12346);
        }

        [TearDown]
        public void Disposing()
        {
            _emptyServer.Dispose();
            _emptyClient.Dispose();
        }


        [Test]
        public async Task Proxy_SayMissingCord_NotBlocks()
        {
            await TestTools.AssertNotBlocks(() => _emptyServer.ClientSideConnection.Contract.Say());
        }

        [Test]
        public async Task Proxy_AskMissingCord_Throws()
        {
            await TestTools.AssertThrowsAndNotBlocks<RemoteContractImplementationException>(() => _emptyServer.ClientSideConnection.Contract.Ask());
            Assert.That(_emptyServer.ClientSideConnection.Channel.IsConnected == _emptyServer.ServerSideConnection.Channel.IsConnected);
        }

        [Test]
        public async Task Proxy_SayAsyncMissingCord_Throws()
        {
            await TestTools.AssertThrowsAndNotBlocks<RemoteContractImplementationException>(() => _emptyServer.ClientSideConnection.Contract.SayAsync());
            Assert.That(_emptyServer.ClientSideConnection.Channel.IsConnected == _emptyServer.ServerSideConnection.Channel.IsConnected);
        }
        [Test]
        public async Task Proxy_AskAsyncMissingCord_Throws()
        {
            await TestTools.AssertThrowsAndNotBlocks<RemoteContractImplementationException>(() => _emptyServer.ClientSideConnection.Contract.AskAsync());
            Assert.That(_emptyServer.ClientSideConnection.Channel.IsConnected == _emptyServer.ServerSideConnection.Channel.IsConnected);
        }



        [Test]
        public async Task Proxy_OnSayMissingCord_NotBlocks()
        {
            await TestTools.AssertNotBlocks(() => _emptyClient.ServerSideConnection.Contract.OnSay());
        }

        [Test]
        public async Task Proxy_OnAskMissingCord_Throws()
        {
            await TestTools.AssertThrowsAndNotBlocks<RemoteContractImplementationException>(() => _emptyClient.ServerSideConnection.Contract.OnAsk());
            Assert.That(_emptyClient.ClientSideConnection.Channel.IsConnected == _emptyClient.ServerSideConnection.Channel.IsConnected);
        }

        [Test]
        public async Task Proxy_OnSayAsyncMissingCord_Throws()
        {
            await TestTools.AssertThrowsAndNotBlocks<RemoteContractImplementationException>(() => _emptyClient.ServerSideConnection.Contract.FuncTaskWithResult());
            Assert.That(_emptyClient.ClientSideConnection.Channel.IsConnected == _emptyClient.ServerSideConnection.Channel.IsConnected);
        }
        [Test]
        public async Task Proxy_OnAskAsyncMissingCord_Throws()
        {
            await TestTools.AssertThrowsAndNotBlocks<RemoteContractImplementationException>(() => _emptyClient.ServerSideConnection.Contract.FuncTaskWithResultAndParam(""));
            Assert.That(_emptyClient.ClientSideConnection.Channel.IsConnected == _emptyClient.ServerSideConnection.Channel.IsConnected);
        }
    }
}
