using CommonTestTools;
using CommonTestTools.Contracts;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TNT.Core.Tests.AsyncTests
{
    [TestFixture]
    public class DefaultAnswersTests
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
        public async Task Origin_FuncTaskNotImplemented_returnsDefault()
        {
            await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.FuncTask());
        }
        [Test]
        public async Task Origin_AsksNotImplemented_returns()
        {
            var answer = await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.FuncTaskWithResultAndParam(""));
            Assert.That(default == answer);
        }
    }
}
