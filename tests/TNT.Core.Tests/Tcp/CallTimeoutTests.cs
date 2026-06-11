using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CommonTestTools;
using NUnit.Framework;
using TheNetTunnel.Api;
using TheNetTunnel.Contract;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Tcp;

namespace TheNetTunnel.Tests.Tcp
{
    public interface IGatedContract
    {
        [TntMessage(1)] int BlockingAsk();
        [TntMessage(2)] Task<int> BlockingAskTask();
    }

    public class GatedContract : IGatedContract
    {
        public readonly ManualResetEventSlim Gate = new(false);

        public int BlockingAsk()
        {
            Gate.Wait(10000);
            return 42;
        }

        public Task<int> BlockingAskTask()
        {
            Gate.Wait(10000);
            return Task.FromResult(42);
        }
    }

    [TestFixture]
    public class CallTimeoutTests
    {
        private const int AnswerTimeoutMs = 500;

        private TntTcpServer<IGatedContract> _server;
        private IConnection<IGatedContract> _client;
        private GatedContract _serverContract;

        [SetUp]
        public async Task SetUp()
        {
            _server = TntBuilder
                .UseContract<IGatedContract, GatedContract>()
                .CreateTcpServer(IPAddress.Loopback, 12412);
            _server.Start();

            var serverSideTask = _server.WaitForAClient();

            _client = await TntBuilder
                .UseContract<IGatedContract>()
                .SetMaxAnsTimeout(AnswerTimeoutMs)
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12412);

            var serverSide = await serverSideTask;
            _serverContract = (GatedContract)serverSide.Contract;
        }

        [TearDown]
        public void TearDown()
        {
            // Release the handler before tearing down, otherwise the dispatcher
            // would wait for it.
            _serverContract.Gate.Set();
            _client.Dispose();
            _server.Dispose();
        }

        [Test]
        public async Task SyncAsk_WhenHandlerHangs_ThrowsCallTimeout()
        {
            await TestTools.AssertThrowsAndNotBlocks<CallTimeoutException>(
                () => _client.Contract.BlockingAsk(),
                timeout: AnswerTimeoutMs * 10);
        }

        [Test]
        public async Task AsyncAsk_WhenHandlerHangs_ThrowsCallTimeout()
        {
            await TestTools.AssertThrowsAndNotBlocks<CallTimeoutException>(
                () => _client.Contract.BlockingAskTask(),
                timeout: AnswerTimeoutMs * 10);
        }

        [Test]
        public async Task LateAnswer_AfterTimeout_DoesNotBreakNextCalls()
        {
            await TestTools.AssertThrowsAndNotBlocks<CallTimeoutException>(
                () => _client.Contract.BlockingAsk(),
                timeout: AnswerTimeoutMs * 10);

            // The late answer arrives after the awaiter was removed and must be ignored.
            _serverContract.Gate.Set();
            await Task.Delay(200);

            Assert.That(_client.Contract.BlockingAsk(), Is.EqualTo(42),
                "The connection must stay usable after a timed-out call");
        }
    }
}
