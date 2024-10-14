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
            //.UseMultiOperationDispatcher()
            .CreateTcpServer(IPAddress.Loopback, 12345);
        
        server.Start();

        Console.WriteLine("Type your messages:");

        try
        {
            using var client = await TntBuilder.UseContract<IExampleContract>()
                .SetMaxAnsTimeout(30000)
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

            //var random = new Random();

            //for (int i = 0; i < 15000; i++)
            //{
            //    var tt = random.Next(50);
            //    var task = SendMsgTask(client, i);
            //}

            ////await Task.Delay(1000);
            //await Task.Delay(-1);

            client.Contract.Action += AAAction;
            client.Contract.Func += AAFunc;
            client.Contract.FuncTask += AASyncFunc;
            client.Contract.FuncTaskResult += AAFuncRes;

            //await client.Contract.SendTask("Superman", $"message#{1}");

            //for (int i = 0; i < 1000; i++)
            //{
            //    var t = i;
            //    _ = Task.Run(() =>
            //    {
            //        contract.Action.Invoke(t);
            //    });
            //}

            //await Task.Delay(-1);

            for (int i = 0; i < 1000; i++)
            {
                var t = i;
                _ = Task.Run(async () =>
                {
                    var res = await contract.FuncTaskResult.Invoke(t);

                    if (!res)
                    {

                    }
                        
                });
            }

            await Task.Delay(-1);
        }
        finally
        {
            server.Dispose();
        }
    }

    private static Task<bool> AAFuncRes(int arg)
    {
        Console.WriteLine($"AAAAA {arg}");

        return Task.FromResult(true);
    }

    private static Task AASyncFunc(int arg)
    {
        Console.WriteLine($"AAAAA {arg}");

        return Task.CompletedTask;
    }

    private static bool AAFunc(int a)
    {
        Console.WriteLine($"AAAAA {a}");

        return true;
    }
    private static void AAAction(int a)
    {
        Console.WriteLine($"AAAAA {a}");
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
    Action<int> Action { get; set; }

    [TntMessage(2)]
    Func<int, bool> Func { get; set; }

    [TntMessage(3)]
    Func<int, Task> FuncTask { get; set; }

    [TntMessage(4)]
    Func<int, Task<bool>> FuncTaskResult { get; set; }

    [TntMessage(11)]
    void Send(string user, string message);
    [TntMessage(12)]
    bool Send1(string user, string message);

    [TntMessage(13)]
    Task SendTask(string user, string message);
    [TntMessage(14)]
    Task<bool> Send1Task(string user, string message);
}

//contract implementation
public class ExampleContract : IExampleContract
{
    public Action<int> Action { get; set; }
    public Func<int, bool> Func { get; set; }
    public Func<int, Task> FuncTask { get; set; }
    public Func<int, Task<bool>> FuncTaskResult { get; set; }

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