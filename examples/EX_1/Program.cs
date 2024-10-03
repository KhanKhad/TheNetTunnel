﻿using System;
using System.Net;
using TNT.Core.Tcp;
using TNT.Core.Api;
using TNT.Core.Contract;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using System.Threading;

namespace EX_1;

static class Program
{
    static async Task Main()
    {
        var server = TntBuilder
            .UseContract<IExampleContract, ExampleContract>()
            .UseMultiOperationDispatcher()
            .CreateTcpServer(IPAddress.Loopback, 12345);
        
        server.Start();

        Console.WriteLine("Type your messages:");

        try
        {
            using var client = await TntBuilder.UseContract<IExampleContract>()
                .SetMaxAnsTimeout(30000)
                .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

            var random = new Random();

            for (int i = 0; i < 15000; i++)
            {
                var tt = random.Next(50);

                var task = SendMsgTask(client, i);

                if (tt != 0)
                    await task;
            }


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

    private static Task SendMsgTask(IConnection<IExampleContract> client, int i)
    {
        return Task.Run(() =>
        {
            try
            {
                //client.Contract.Send("Superman", $"message#{i}");

                var res = client.Contract.Send1("Superman", $"message#{i}");

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
    public bool Send1(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
        return true;
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