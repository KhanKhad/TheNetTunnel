using TNT;

namespace TNT.Benchmarks
{
    /// <summary>
    /// Minimal contract exercised by the benchmarks. Master only supports
    /// synchronous and fire-and-forget calls (any non-void return is treated as
    /// a synchronous Ask&lt;T&gt;), so there is no async echo here — that is a
    /// newLine-only capability.
    /// </summary>
    public interface IBenchContract
    {
        [TntMessage(1)] bool Ping();
        [TntMessage(2)] byte[] Echo(byte[] data);
        [TntMessage(4)] void Fire(byte[] data);
    }

    public class BenchContract : IBenchContract
    {
        public bool Ping() => true;
        public byte[] Echo(byte[] data) => data;
        public void Fire(byte[] data) { }
    }
}
