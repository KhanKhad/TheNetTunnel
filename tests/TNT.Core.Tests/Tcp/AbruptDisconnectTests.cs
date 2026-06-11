using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommonTestTools;
using CommonTestTools.Contracts;
using NUnit.Framework;

namespace TheNetTunnel.Tests.Tcp
{
    /// <summary>
    /// Verifies that a dropped transport is detected by the receive loop itself
    /// (zero-byte read / socket error), without waiting for ping send failures.
    /// Timeouts are below the 5s ping interval on purpose: if detection relied
    /// on pings, these tests would fail.
    /// </summary>
    [TestFixture]
    public class AbruptDisconnectTests
    {
        private const int DetectionTimeoutMs = 3000;

        [Test]
        public async Task ClientChannelClosed_ServerDetectsDisconnect()
        {
            ServerAndClient<ITestContract, ITestContract, TestContractMock> serverAndClient = null;
            try
            {
                serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.CreateAsync(12399);

                var channelDisconnectRaised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                serverAndClient.ServerSideConnection.Channel.OnDisconnect += (_, _) => channelDisconnectRaised.TrySetResult();

                var serverDisconnectedRaised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                serverAndClient.TntTcpServer.Disconnected += (_, _) => serverDisconnectedRaised.TrySetResult();

                // Kill the client transport without notifying the server.
                serverAndClient.ClientSideConnection.Channel.Dispose();

                var completed = await Task.WhenAny(channelDisconnectRaised.Task, Task.Delay(DetectionTimeoutMs));
                Assert.That(completed, Is.EqualTo(channelDisconnectRaised.Task),
                    "Server-side channel did not detect the disconnect");

                completed = await Task.WhenAny(serverDisconnectedRaised.Task, Task.Delay(DetectionTimeoutMs));
                Assert.That(completed, Is.EqualTo(serverDisconnectedRaised.Task),
                    "Server Disconnected event was not raised");
            }
            finally
            {
                serverAndClient?.Dispose();
            }
        }

        [Test]
        public async Task ServerChannelClosed_ClientCallsFailFast()
        {
            ServerAndClient<ITestContract, ITestContract, TestContractMock> serverAndClient = null;
            try
            {
                serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.CreateAsync(12399);

                var channelDisconnectRaised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                serverAndClient.ClientSideConnection.Channel.OnDisconnect += (_, _) => channelDisconnectRaised.TrySetResult();

                // Kill the server transport without notifying the client.
                serverAndClient.ServerSideConnection.Channel.Dispose();

                var completed = await Task.WhenAny(channelDisconnectRaised.Task, Task.Delay(DetectionTimeoutMs));
                Assert.That(completed, Is.EqualTo(channelDisconnectRaised.Task),
                    "Client-side channel did not detect the disconnect");

                // The interlocutor must shut down right after the channel completes,
                // so calls fail immediately instead of waiting for the 30s answer timeout.
                Exception failure = null;
                var sw = Stopwatch.StartNew();

                while (failure == null && sw.ElapsedMilliseconds < DetectionTimeoutMs)
                {
                    try
                    {
                        serverAndClient.ClientSideConnection.Contract.Ask();
                        await Task.Delay(50);
                    }
                    catch (Exception e)
                    {
                        failure = e;
                    }
                }

                Assert.That(failure, Is.Not.Null, "Client call did not fail after the transport was closed");
            }
            finally
            {
                serverAndClient?.Dispose();
            }
        }
    }
}
