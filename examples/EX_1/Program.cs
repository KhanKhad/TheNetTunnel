using System;
using System.Net;
using TNT.Core.Tcp;
using TNT.Core.Api;
using TNT.Core.Contract;
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
        var server = TntBuilder
            .UseContract<IExampleContract, ExampleContract>()
            //.UseMultiOperationDispatcher()
            .CreateTcpServer(IPAddress.Loopback, 12345);
        
        server.Start();

        Console.WriteLine("Type your messages:");

        try
        {
            using var client = await TntBuilder.UseContract<IExampleContract>()
                .SetMaxAnsTimeout(30000)
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

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

            var firstClient = await server.WaitForAClient();

            for (int i = 0; i < 1000; i++)
            {
                var t = i;
                _ = Task.Run(async () =>
                {
                    var res = await firstClient.Contract.FuncTaskResult.Invoke(t, 1);

                    if (!res)
                    {

                    }

                });
            }

            var secondClientTask = server.WaitForAClient(true);

            using var client2 = await TntBuilder.UseContract<IExampleContract>()
                .SetMaxAnsTimeout(30000)
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

            client2.Contract.Action += AAAction;
            client2.Contract.Func += AAFunc;
            client2.Contract.FuncTask += AASyncFunc;
            client2.Contract.FuncTaskResult += AAFuncRes;

            var secondClient = await secondClientTask;

            for (int i = 0; i < 1000; i++)
            {
                var t = i;
                _ = Task.Run(async () =>
                {
                    var res = await secondClient.Contract.FuncTaskResult.Invoke(t, 2);

                    if (!res)
                    {

                    }

                });
            }

            for (int i = 0; i < 1000; i++)
            {
                var t = i;
                _ = Task.Run(async () =>
                {
                    var res = await firstClient.Contract.FuncTaskResult.Invoke(t, 1);

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

    private static Task<bool> AAFuncRes(int arg, int arg2)
    {
        Console.WriteLine($"AAAAA {arg}|{arg2}");

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
    Func<int,int, Task<bool>> FuncTaskResult { get; set; }

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
    public Func<int, int, Task<bool>> FuncTaskResult { get; set; }

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