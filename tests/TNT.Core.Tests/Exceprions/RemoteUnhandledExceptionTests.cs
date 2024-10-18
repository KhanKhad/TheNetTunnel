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
    public class RemoteUnhandledExceptionTests
    {
        private ServerAndClient<ITestContract, ITestContract, TestContractMock> _serverAndClient;

        [SetUp]
        public async Task SetUp()
        {
            _serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.Create();
        }

        [TearDown]
        public void Disposing()
        {
            _serverAndClient.Dispose();
        }


        [Test]
        public async Task Proxy_SayWithException_CallNotThrows()
        {
            await TestTools.AssertNotBlocks(() => _serverAndClient
            .ClientSideConnection.Contract.SayWithException());
        }
        [Test]
        public async Task Proxy_AskUserUnhandled_Throws()
        {
            await TestTools.AssertThrowsAndNotBlocks<RemoteUnhandledException>(() => _serverAndClient
            .ClientSideConnection.Contract.AskWithException("asd"));
        }
        [Test]
        public async Task Proxy_SayAsyncUserUnhandled_Throws()
        {
            await TestTools.AssertThrowsAndNotBlocks<RemoteUnhandledException>(() => _serverAndClient
            .ClientSideConnection.Contract.SayWithExceptionAsync("thjg"));
        }
        [Test]
        public async Task Proxy_AskAsyncUserUnhandled_Throws()
        {
            await TestTools.AssertThrowsAndNotBlocks<RemoteUnhandledException>(() => _serverAndClient
            .ClientSideConnection.Contract.AskWithExceptionAsync());
        }


        [Test]
        public async Task Proxy_OnSayWithException_CallNotThrows()
        {
            _serverAndClient.ClientSideConnection.Contract.OnSay += () => throw new Exception();

            await TestTools.AssertNotBlocks(() => _serverAndClient
            .ServerSideConnection.Contract.OnSay());
        }
        [Test]
        public async Task Proxy_OnAskUserUnhandled_Throws()
        {
            _serverAndClient.ClientSideConnection.Contract.OnAsk += () => throw new Exception();

            await TestTools.AssertThrowsAndNotBlocks<RemoteUnhandledException>(() => _serverAndClient
            .ServerSideConnection.Contract.OnAsk());
        }
        [Test]
        public async Task Proxy_FuncTaskUserUnhandled_Throws()
        {
            _serverAndClient.ClientSideConnection.Contract.FuncTaskWithResult += () => throw new Exception();

            await TestTools.AssertThrowsAndNotBlocks<RemoteUnhandledException>(() => _serverAndClient
            .ServerSideConnection.Contract.FuncTaskWithResult());
        }
        [Test]
        public async Task Proxy_FuncTaskWithResultUserUnhandled_Throws()
        {
            _serverAndClient.ClientSideConnection.Contract.FuncTaskWithResultAndParam += (a) => throw new Exception();

            await TestTools.AssertThrowsAndNotBlocks<RemoteUnhandledException>(() => _serverAndClient
            .ServerSideConnection.Contract.FuncTaskWithResultAndParam(""));
        }
    }
}
