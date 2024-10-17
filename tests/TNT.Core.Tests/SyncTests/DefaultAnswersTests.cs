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
    public class DefaultAnswersTests
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
        public async Task Origin_AsksNotImplemented_returnsDefault()
        {
            var answer = await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.OnAsk());
            Assert.That(default == answer);
        }
    }
}
