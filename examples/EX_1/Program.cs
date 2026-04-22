using System;
using System.Net;
using TheNetTunnel.Tcp;
using TheNetTunnel.Api;
using TheNetTunnel.Contract;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Diagnostics;
using System.Linq;

namespace EX_1;

static class Program
{
    static async Task Main()
    {
        using var server = TntBuilder
            .UseContract<IExampleContract, ExampleContract>()
            //.UseMultiOperationDispatcher()
            .CreateTcpServer(IPAddress.Loopback, 12345);
        
        server.Start();

        var firstClientTask = server.WaitForAClient();

        using var client = await TntBuilder.UseContract<IExampleContract>()
                .SetMaxAnsTimeout(30000)
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

        await firstClientTask;

        Console.WriteLine("Type your messages:");
        
        while (true)
        {
            var message = Console.ReadLine();
            client.Contract.Send("Superman", message);
        }
    }
}

//contract
[TntMinimalServerVersion(1, 0, 0)]
[TntMinimalClientVersion(1, 0, 0)]
[TntClientVersion(1, 0, 0)]
[TntServerVersion(1, 0, 0)]
public interface IExampleContract
{
    [TntMessage(1)] Action<int> Action { get; set; }
    [TntMessage(2)] Func<int, bool> Func { get; set; }
    [TntMessage(3)] Func<int, Task> FuncTask { get; set; }
    [TntMessage(4)] Func<int,int, Task<bool>> FuncTaskWithResult { get; set; }

    [TntMessage(11)] void Send(string user, string message);
    [TntMessage(12)] bool SendWithResult(string user, string message);
    [TntMessage(13)] Task SendTask(string user, string message);
    [TntMessage(14)] Task<bool> SendTaskWithResult(string user, string message);
}

//contract implementation
public class ExampleContract : IExampleContract, IDisposable
{
    public Action<int> Action { get; set; }
    public Func<int, bool> Func { get; set; }
    public Func<int, Task> FuncTask { get; set; }
    public Func<int, int, Task<bool>> FuncTaskWithResult { get; set; }

    public ExampleContract()
    {

    }

    public void Send(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
    }
    public bool SendWithResult(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
        return true;
    }

    public Task<bool> SendTaskWithResult(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
        return Task.FromResult(true);
    }

    public Task SendTask(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        
    }
}