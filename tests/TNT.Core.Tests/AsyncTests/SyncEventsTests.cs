using CommonTestTools.Contracts;
using CommonTestTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace TNT.Core.Tests.AsyncTests
{
    [TestFixture]
    public class AsyncEventsTests
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
        public async Task FuncTaskTest()
        {
            var res = false;
            _serverAndClient.ClientSideConnection.Contract.FuncTask += () => { res = true; return Task.CompletedTask; };

            await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.FuncTask());

            Assert.That(res);
        }

        [TestCase(0)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public async Task FuncTaskWithResultTest(int msg)
        {
            _serverAndClient.ClientSideConnection.Contract.FuncTaskWithResult += () => { return Task.FromResult(msg); };

            var res = await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.FuncTaskWithResult());

            Assert.That(res == msg);
        }

        [TestCase("Hey you")]
        [TestCase("")]
        [TestCase(null)]
        public async Task FuncTaskWithResultAndParamTest(string msg)
        {
            _serverAndClient.ClientSideConnection.Contract.FuncTaskWithResultAndParam += (a) => { return Task.FromResult(a); };

            var res = await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.FuncTaskWithResultAndParam(msg));

            Assert.That(res == msg);
        }


        [TestCase("Hey you", 12, 24)]
        [TestCase("", 234, 0)]
        [TestCase(null, 0, long.MaxValue)]
        public async Task FuncTaskWithResultILTest(string s, int i, long l)
        {
            var func = new Func<string, int, long, Task<string>>((s1, i2, l3) =>
            {
                return Task.FromResult(s1 + i2.ToString() + l3.ToString());
            });

            _serverAndClient.ClientSideConnection.Contract.FuncTaskWithResultIL += func;

            var res = await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.FuncTaskWithResultIL(s, i, l));

            var originResult = await func(s, i, l);

            Assert.That(res == originResult);
        }
    }
}
