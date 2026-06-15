using System.Reflection;
using BenchmarkDotNet.Running;

namespace TNT.Benchmarks
{
    public static class Program
    {
        // Run all:        dotnet run -c Release --project tests/TNT.Benchmarks -- --filter *
        // Filter:         dotnet run -c Release --project tests/TNT.Benchmarks -- --filter *Echo*
        // Quick smoke:    dotnet run -c Release --project tests/TNT.Benchmarks -- --job short --filter *
        public static void Main(string[] args)
            => BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
    }
}
