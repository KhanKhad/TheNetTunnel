# TheNetTunnel
Tcp Client Server rpc library with binary serialization. Protobuf and or custom serialization is supporting out-of-box.

TNT is supposed for being fast compact and easy to use library. Watch speed test results below.   

To install TNT, run the following command in the Package Manager Console:

```
PM> Install-Package tnt
```

# Example
```
void main(){
 var server = TntBuilder
      .UseContract<IExampleContract, ExampleContract>()
      .CreateTcpServer(IPAddress.Any, 12345);
  server.StartListening();

  Console.WriteLine("Type your messages:");
  while (true) {
      var message = Console.ReadLine();
      using (var client = TntBuilder.UseContract<IExampleContract>()
          .CreateTcpClientConnection(IPAddress.Loopback, 12345))
      {
         client.Contract.Send("Superman", message);
      }  
  }
  server.Close();
}

//contract
public interface IExampleContract     {
    [TntMessage(1)] void Send(string user, string message);
}
//contract implementation
public class ExampleContract : IExampleContract     {
    public void Send(string user, string message)         {
        Console.WriteLine($"[Server received:] {user} : {message}");
    }
}
```

Other examples: 

- [Stage 1. Easy start](https://github.com/tmteam/TheNetTunnel/blob/master/src/Example/Stage1_EasyStart/Stage1_EasyStartExample.cs)
- [Stage 2. Diehard example](https://github.com/tmteam/TheNetTunnel/blob/master/src/Example/Stage2_ComplexExample/Stage2_Example.cs)
- [Stage 3. Let's test!](https://github.com/tmteam/TheNetTunnel/blob/master/src/Example/Stage3_IntroducingToTestingExample/Stage3_Example.cs)

# Speed test results

It's hard to name a single figure, otrageous overall TNT speed if you're not a marketer. There are many such numbers.
All results are performed by [local speed test](https://github.com/tmteam/TheNetTunnel/tree/master/src/TNT.SpeedTest) 
```

General speed results:
no serialization: 2.5 - 5 Gbit
protobuff serialization: 0.4 - 1 GBit



Output speed:
[Sending in "fire and forget" style]

Raw byte Array:       up to 2300 megabytes/sec
String serialization: up to 830 megabytes/sec
Protobuff serialization: up to 120 megabytes/sec

Echo transaction speed:
[Send data and received its copy]

Raw byte Array:       up to 300 megabytes/sec
String serialization: up to 188 megabytes/sec
Protobuff serialization: up to 47 megabytes/sec


Overheads:

Output localhost Delay: 9,06 microseconds
Output message overhead: 6 byte per message

Echo transaction localhost (IO) Delay: 34,87 microseconds
Echo transaction (I/O) overhead : 8/12 byte per message
```

# Benchmarks (BenchmarkDotNet)

For rigorous, warmed-up per-call numbers there is a BenchmarkDotNet project at
[tests/TNT.Benchmarks](tests/TNT.Benchmarks). It measures real loopback RPC
round-trips across payload sizes. Master supports synchronous and
fire-and-forget calls (any non-void return is a synchronous `Ask<T>`), so the
benchmarks cover `SyncPing` and `SyncEcho`.

```
dotnet run -c Release --project tests/TNT.Benchmarks -- --filter *              # full run
dotnet run -c Release --project tests/TNT.Benchmarks -- --job short --filter *  # quick
```

Full run on AMD Ryzen 7 5700G, Windows 10, .NET 6 (BenchmarkDotNet v0.13.12),
loopback TCP:

| Method   | Payload | Mean      | Error    | Allocated |
|----------|---------|-----------|----------|-----------|
| SyncPing | –       |  80.69 µs | 0.403 µs |   3.27 KB |
| SyncEcho | 0       |  67.06 µs | 0.657 µs |   3.15 KB |
| SyncEcho | 1 KB    |  70.33 µs | 0.348 µs |  13.24 KB |
| SyncEcho | 64 KB   | 137.47 µs | 2.468 µs | 515.32 KB |

A small request/response round-trip is ~67–81 µs and largely payload-independent
up to ~1 KB — it is latency-bound (thread hops + scheduling), not data-bound.
Above that, serialization/copy of the payload dominates. Note the echo path
allocates ~8× the payload at 64 KB (multiple buffer copies).

