using System;
using System.Net;
using TNT.Core.Tcp;
using TNT.Core.Api;
using TNT.Core.Contract;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Diagnostics;

namespace EX_1;

static class Program
{
    static async Task Main()
    {
        var contract = new ExampleContract();
        var server = TntBuilder
            .UseContract<IExampleContract>(contract)
            .UseMultiOperationDispatcher()
            .CreateTcpServer(IPAddress.Loopback, 12345);
        
        server.Start();

        Console.WriteLine("Type your messages:");

        try
        {
            using var client = await TntBuilder.UseContract<IExampleContract>()
                .SetMaxAnsTimeout(300000)
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

            //var random = new Random();

            //for (int i = 0; i < 15000; i++)
            //{
            //    var tt = random.Next(50);
            //    var task = SendMsgTask(client, i);
            //}

            await Task.Delay(1000);

            client.Contract.Action += AA;


            //await client.Contract.SendTask("Superman", $"message#{1}");

            contract.Action.Invoke(true);
            //await client.Contract.SendTask("Superman", $"message#{1}");

            //for (int i = 0; i < 100; i++)
            //{
            //    var task = SendMsgTask(client, i);
            //}

            await Task.Delay(-1);
        }
        finally
        {
            server.Dispose();
        }
    }

    private static void AA(bool a)
    {
        Console.WriteLine("AAAAA");
    }


    private static Task SendMsgTask(IConnection<IExampleContract> client, int i)
    {
        return Task.Run(async () =>
        {
            try
            {
                //client.Contract.Send("Superman", $"message#{i}");

                var res = await client.Contract.Send1Task("Superman", $"message#{i}");

                if (res != true)
                    throw new Exception();
            }
            catch (Exception ex) 
            {

            }
        });
    }
}

//contract
public interface IExampleContract
{
    [TntMessage(1)]
    void Send(string user, string message);
    [TntMessage(2)]
    bool Send1(string user, string message);

    [TntMessage(3)]
    Action<bool> Action { get; set; }

    //[TntMessage(4)]
    //Func<bool, bool> Func { get; set; }



    [TntMessage(11)]
    Task SendTask(string user, string message);
    [TntMessage(12)]
    Task<bool> Send1Task(string user, string message);

    /*[TntMessage(13)]
    Func<Task<bool>> FuncTask { get; set; }

    [TntMessage(14)]
    Func<bool, Task<bool>> FuncTaskResult { get; set; }*/
}

//contract implementation
public class ExampleContract : IExampleContract
{
    public Action<bool> Action { get; set; }
    public Func<bool, bool> Func { get; set; }
    //public Func<Task<bool>> FuncTask { get; set; }
    //public Func<bool, Task<bool>> FuncTaskResult { get; set; }

    public ExampleContract()
    {

    }

    public void Send(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
    }
    public bool Send1(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
        return true;
    }

    public Task<bool> Send1Task(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
        return Task.FromResult(true);
    }

    public Task SendTask(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
        return Task.CompletedTask;
    }
}