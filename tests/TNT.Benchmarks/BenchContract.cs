using System.Threading.Tasks;
using TheNetTunnel.Contract;

namespace TNT.Benchmarks
{
    /// <summary>
    /// Minimal contract exercised by the benchmarks: a tiny sync round-trip
    /// (<see cref="Ping"/>), a sync echo and an async echo for payload sizing.
    /// </summary>
    public interface IBenchContract
    {
        [TntMessage(1)] bool Ping();
        [TntMessage(2)] byte[] Echo(byte[] data);
        [TntMessage(3)] Task<byte[]> EchoAsync(byte[] data);
        [TntMessage(4)] void Fire(byte[] data);
    }

    public class BenchContract : IBenchContract
    {
        public bool Ping() => true;
        public byte[] Echo(byte[] data) => data;
        public Task<byte[]> EchoAsync(byte[] data) => Task.FromResult(data);
        public void Fire(byte[] data) { }
    }
}
