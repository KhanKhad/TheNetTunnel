using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TheNetTunnel.ReceiveDispatching;

namespace TheNetTunnel.Tests.DispatcherTests
{
    [TestFixture]
    public class ReceiveDispatcherDisposeTests
    {
        public class GatedContract
        {
            public readonly ManualResetEventSlim Gate = new(false);
            public volatile bool SecondHandlerExecuted;

            public void SlowHandler() => Gate.Wait(10000);
            public void SecondHandler() => SecondHandlerExecuted = true;
        }

        [Test]
        public async Task Dispose_CancelsQueuedTasks_AndDoesNotHang()
        {
            var contract = new GatedContract();

            var dispatcher = new ReceiveDispatcher();
            dispatcher.SetContract(contract);
            dispatcher.Start();

            var slowHandlerInfo = typeof(GatedContract).GetMethod(nameof(GatedContract.SlowHandler));
            var secondHandlerInfo = typeof(GatedContract).GetMethod(nameof(GatedContract.SecondHandler));

            // The first task occupies the single-operation read loop,
            // the second stays queued in the tasks channel.
            var firstCall = dispatcher.HandleSyncSayMessage(slowHandlerInfo, Array.Empty<object>());
            var secondCall = dispatcher.HandleSyncSayMessage(secondHandlerInfo, Array.Empty<object>());

            var disposeTask = Task.Run(() => dispatcher.Dispose());

            // Let Dispose request the shutdown, then release the running handler.
            await Task.Delay(200);
            contract.Gate.Set();

            var completed = await Task.WhenAny(disposeTask, Task.Delay(5000));
            Assert.That(completed, Is.EqualTo(disposeTask), "Dispose is blocked");

            // The running handler finished normally...
            var firstCompleted = await Task.WhenAny(firstCall, Task.Delay(1000));
            Assert.That(firstCompleted, Is.EqualTo(firstCall), "First call did not complete");

            // ...while the queued one was cancelled instead of being executed or abandoned.
            Assert.ThrowsAsync<TaskCanceledException>(() => secondCall);
            Assert.That(contract.SecondHandlerExecuted, Is.False, "Queued handler must not run after Dispose");
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var dispatcher = new ReceiveDispatcher();
            dispatcher.SetContract(new GatedContract());
            dispatcher.Start();

            Assert.DoesNotThrow(() =>
            {
                dispatcher.Dispose();
                dispatcher.Dispose();
            });
        }
    }
}
