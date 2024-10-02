using System;
using System.Net;
using TNT.Core.Tcp;
using TNT.Core.Api;
using TNT.Core.Contract;
using System.Threading.Tasks;

namespace EX_1;

static class Program
{
    static async Task Main()
    {
        var server = TntBuilder
            .UseContract<IExampleContract, ExampleContract>()
            .CreateTcpServer(IPAddress.Loopback, 12345);
        
        server.Start();

        Console.WriteLine("Type your messages:");

        try
        {
            using var client = TntBuilder.UseContract<IExampleContract>()
                    .CreateTcpClientConnection(IPAddress.Loopback, 12345);

            while (true)
            {
                var message = Console.ReadLine();
                client.Contract.Send("Superman", message);
            }
        }
        finally
        {
            server.Dispose();
        }
    }

}

//contract
public interface IExampleContract
{
    [TntMessage(1)]
    void Send(string user, string message);
    [TntMessage(2)]
    void Send1(string user, string message);
    [TntMessage(3)] 
    void Send2(string user, string message);
    [TntMessage(4)] 
    void Send3(string user, string message);
}

//contract implementation
public class ExampleContract : IExampleContract
{
    public void Send(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
    }
    public void Send1(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
    }
    public void Send2(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
    }
    public void Send3(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
    }
}