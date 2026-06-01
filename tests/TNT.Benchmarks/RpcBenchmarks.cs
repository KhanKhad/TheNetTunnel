using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using TheNetTunnel.Api;
using TheNetTunnel.Tcp;

namespace TNT.Benchmarks
{
    /// <summary>
    /// Spins up a loopback TNT server + client over real TCP and keeps the
    /// connection alive for the lifetime of the benchmark instance.
    /// </summary>
    public abstract class RpcBenchmarkBase
    {
        private TntTcpServer<IBenchContract> _server;
        protected IConnection<IBenchContract> Client;

        protected void StartConnection()
        {
            var port = GetFreePort();

            _server = TntBuilder
                .UseContract<IBenchContract, BenchContract>()
                .CreateTcpServer(IPAddress.Loopback, port);
            _server.Start();

            Client = TntBuilder
                .UseContract<IBenchContract>()
                .CreateTcpClientConnection(IPAddress.Loopback, port);

            // Ensure the server side has accepted before measuring.
            _server.WaitForAClient().Wait(5000);

            // Warm the proxy/dispatcher/JIT paths.
            Client.Contract.Ping();
        }

        protected void StopConnection()
        {
            Client?.Dispose();
            _server?.Dispose();
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    /// <summary>Pure synchronous request/response latency floor (empty payload).</summary>
    [MemoryDiagnoser]
    public class LatencyBenchmarks : RpcBenchmarkBase
    {
        [GlobalSetup] public void Setup() => StartConnection();
        [GlobalCleanup] public void Cleanup() => StopConnection();

        [Benchmark] public bool SyncPing() => Client.Contract.Ping();
    }

    /// <summary>Round-trip echo throughput across payload sizes, sync and async.</summary>
    [MemoryDiagnoser]
    public class EchoBenchmarks : RpcBenchmarkBase
    {
        [Params(0, 1024, 65536)] public int PayloadSize;

        private byte[] _payload;

        [GlobalSetup]
        public void Setup()
        {
            StartConnection();
            _payload = new byte[PayloadSize];
            new Random(PayloadSize).NextBytes(_payload);
        }

        [GlobalCleanup] public void Cleanup() => StopConnection();

        [Benchmark] public byte[] SyncEcho() => Client.Contract.Echo(_payload);

        [Benchmark] public Task<byte[]> AsyncEcho() => Client.Contract.EchoAsync(_payload);
    }
}
