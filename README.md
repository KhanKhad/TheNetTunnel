# TheNetTunnel (TNT)

TCP client/server RPC library for .NET with compact binary serialization. You define a plain C# interface — the *contract* — and TNT lets both sides call each other's methods as if they were local. Protobuf and custom serialization are supported out of the box.

TNT is designed to be **fast, compact and easy to use**. See the speed-test numbers below.

- Target framework: **.NET 6**
- Serialization: **protobuf-net** + pluggable custom (de)serializers
- Calls: synchronous, asynchronous (`Task`/`Task<T>`), fire-and-forget, and server→client callbacks

## Installation

```
PM> Install-Package TheNetTunnel
```

## Quick start

Define a contract interface shared by both sides, mark each member with `[TntMessage(id)]`, and provide an implementation on the side that should handle the calls.

```csharp
//contract — shared between client and server
public interface IExampleContract
{
    [TntMessage(1)] void Send(string user, string message);
    [TntMessage(2)] Task<bool> SendWithResult(string user, string message);
}

//contract implementation — lives on the server
public class ExampleContract : IExampleContract
{
    public void Send(string user, string message)
        => Console.WriteLine($"[Server received] {user}: {message}");

    public Task<bool> SendWithResult(string user, string message)
    {
        Console.WriteLine($"[Server received] {user}: {message}");
        return Task.FromResult(true);
    }
}
```

```csharp
using System.Net;
using TheNetTunnel.Api;
using TheNetTunnel.Tcp;

// --- server ---
using var server = TntBuilder
    .UseContract<IExampleContract, ExampleContract>()
    .CreateTcpServer(IPAddress.Any, 12345);

server.Start();

// --- client ---
using var client = TntBuilder
    .UseContract<IExampleContract>()
    .CreateTcpClientConnection(IPAddress.Loopback, 12345);

// call the remote method through the generated proxy
client.Contract.Send("Superman", "hello");
bool ok = await client.Contract.SendWithResult("Superman", "hi again");
```

On the server side you can wait for and enumerate connections:

```csharp
IConnection<IExampleContract> connection = await server.WaitForAClient();
foreach (var c in server.GetAllConnections()) { /* ... */ }
```

> Tip: `WaitForAClient()` returns the next accepted connection. Call it in a loop if you want to observe every client, or subscribe to the `Disconnected` event.

## Calling styles

The return type of a contract method decides how the call behaves:

| Signature              | Behaviour                                                        |
|------------------------|-----------------------------------------------------------------|
| `void Foo(...)`        | Fire-and-forget (no waiting for a reply)                         |
| `T Foo(...)`           | Synchronous request/response, blocks until the answer arrives   |
| `Task Foo(...)`        | Asynchronous fire-and-forget that awaits remote completion      |
| `Task<T> Foo(...)`     | Asynchronous request/response                                   |

Server→client **callbacks** are expressed as `Action<...>` / `Func<...>` properties on the contract:

```csharp
public interface IExampleContract
{
    [TntMessage(10)] Action<int> OnTick { get; set; }
    [TntMessage(11)] Func<int, Task<bool>> AskClient { get; set; }
}
```

Use the async connection factory and `*Async` calls to stay non-blocking end to end:

```csharp
using var client = await TntBuilder
    .UseContract<IExampleContract>()
    .SetMaxAnsTimeout(30_000) // ms to wait for a response before timing out
    .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);
```

## Versioning & handshake

A handshake runs on connect. Decorate the contract to advertise and enforce compatible versions; a client that doesn't satisfy the server's minimum (or vice versa) is rejected before any traffic flows:

```csharp
[TntClientVersion(1, 0, 0)]
[TntServerVersion(1, 0, 0)]
[TntMinimalClientVersion(1, 0, 0)]
[TntMinimalServerVersion(1, 0, 0)]
public interface IExampleContract { /* ... */ }
```

## Customizing the connection

`ContractBuilder` is fluent:

```csharp
TntBuilder.UseContract<IExampleContract, ExampleContract>()
    .SetMaxAnsTimeout(30_000)        // response timeout, ms
    .UseSingleOperationDispatcher()  // handle one incoming call at a time (default)
    // .UseMultiOperationDispatcher() // handle incoming calls concurrently
    .UseSerializer(myRule)           // plug a custom serializer
    .UseDeserializer(myRule)         // plug a custom deserializer
    .CreateTcpServer(IPAddress.Any, 12345);
```

- **Dispatcher mode** — single-operation processes incoming calls sequentially (ordered, simplest to reason about); multi-operation runs them concurrently.
- **Custom serialization** — register `SerializationRule` / `DeserializationRule` to handle your own types alongside the built-in primitives and protobuf.
- **Connection limit** — `CreateTcpServer(ip, port, maxConnections)` rejects clients past the limit during the handshake.

## Diagnostics

Background loops (socket read, ping heartbeat, dispatcher) never throw into your code. To observe internal errors that would otherwise be silent, assign a logger:

```csharp
using TheNetTunnel.Diagnostics;

TntLog.Logger = new MyLogger(); // implements ITntLogger
```

## Robustness

Incoming frames are length-prefixed. A declared frame length that is negative or exceeds the limit (`ReceivePduQueue.DefaultMaxFrameLength`, 64 MB) is rejected before any allocation, and the connection is dropped — protecting the server from malformed or hostile length prefixes.

## Examples

- [EX_1 — minimal client/server](examples/EX_1/Program.cs)
- [EX_2 / Stage 1 — easy start](examples/EX_2/Stage1_EasyStart/Stage1_EasyStartExample.cs)
- [EX_2 / Stage 2 — complex example](examples/EX_2/Stage2_ComplexExample)
- [EX_2 / Stage 3 — introducing testing](examples/EX_2/Stage3_IntroducingToTestingExample)

## Speed test results

Produced by the [local speed test](tests/TNT.LocalSpeedTest) over a loopback TCP
connection (single desktop machine, .NET 6, Release). Run it yourself with
`dotnet run -c Release --project tests/TNT.LocalSpeedTest`. Throughput depends
heavily on the serializer; numbers below are representative peaks:

```
Output ("fire and forget", client → server):
  raw byte array:          up to ~2300 MB/s (send), ~2200 MB/s sustained
  string serialization:    up to ~1600 MB/s (send), ~1550 MB/s sustained
  protobuf serialization:  up to  ~245 MB/s

Echo transaction (send data and receive its copy):
  raw byte array:          up to ~2000 MB/s
  string serialization:    up to  ~650 MB/s
  protobuf serialization:  up to  ~100 MB/s

Overhead (localhost):
  output delay:                  ~8–12 µs
  echo transaction round-trip:   ~86 µs
  per-message overhead:          12 bytes (output), 12/13 bytes (echo)
```

> These are localhost numbers and will vary with hardware, packet size and the
> serializer. Re-run the local speed test for figures on your own environment.

### Benchmarks (BenchmarkDotNet)

For rigorous, warmed-up per-call numbers there is a BenchmarkDotNet project at
[tests/TNT.Benchmarks](tests/TNT.Benchmarks). It measures real loopback RPC
round-trips (sync and async) across payload sizes:

```
dotnet run -c Release --project tests/TNT.Benchmarks            # full run
dotnet run -c Release --project tests/TNT.Benchmarks -- --job short --filter *   # quick
```

Full run on AMD Ryzen 7 5700G, Windows 10, .NET 6 (BenchmarkDotNet v0.13.12),
loopback TCP:

| Benchmark | Payload | Mean      | Error    | Allocated |
|-----------|---------|-----------|----------|-----------|
| SyncPing  | –       |  84.90 µs | 0.56 µs  |  2.06 KB  |
| SyncEcho  | 0       |  83.38 µs | 0.62 µs  |  2.02 KB  |
| AsyncEcho | 0       |  85.35 µs | 0.72 µs  |  2.62 KB  |
| SyncEcho  | 1 KB    |  85.85 µs | 0.71 µs  |  4.07 KB  |
| AsyncEcho | 1 KB    |  87.98 µs | 1.29 µs  |  4.66 KB  |
| SyncEcho  | 64 KB   | 128.07 µs | 0.94 µs  | 130.15 KB |
| AsyncEcho | 64 KB   | 133.85 µs | 1.15 µs  | 130.74 KB |

A small request/response round-trip is ~85 µs and essentially payload-independent
up to ~1 KB — i.e. it is latency-bound (thread hops + scheduling), not data-bound.
Above that, serialization/copy of the payload starts to dominate. Fixed per-call
allocation is ~2 KB; the echo cases add ~2× the payload (serialize out +
deserialize back).
