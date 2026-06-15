using System;
using System.Net;
using System.Net.Sockets;
using BenchmarkDotNet.Attributes;
using TNT;
using TNT.Api;
using TNT.Transport;

namespace TNT.Benchmarks
{
    /// <summary>
    /// Spins up a loopback TNT server + client over real TCP and keeps the
    /// connection alive for the lifetime of the benchmark instance.
    /// Mirrors the newLine TNT.Benchmarks project, adapted to the master API
    /// (TntBuilder / StartListening / IConnection&lt;TContract,TChannel&gt;).
    /// </summary>
    public abstract class RpcBenchmarkBase
    {
        private IDisposable _server;
        protected IConnection<IBenchContract, IChannel> Client;

        protected void StartConnection()
        {
            var port = GetFreePort();

            var server = TntBuilder
                .UseContract<IBenchContract, BenchContract>()
                .CreateTcpServer(IPAddress.Loopback, port);
            server.StartListening();
            _server = server;

            Client = TntBuilder
                .UseContract<IBenchContract>()
                .CreateTcpClientConnection(IPAddress.Loopback, port);

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

    /// <summary>Round-trip echo throughput across payload sizes (sync only on master).</summary>
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
    }
}
