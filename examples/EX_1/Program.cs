﻿using System;
using System.Net;
using TNT.Core.Tcp;
using TNT.Core.Api;
using TNT.Core.Contract;

namespace EX_1;

static class Program
{
    static void Main()
    {
        var server = TntBuilder
            .UseContract<IExampleContract, ExampleContract>()
            .CreateTcpServer(IPAddress.Loopback, 12345);
        server.StartListening();

        Console.WriteLine("Type your messages:");

        try
        {
            while (true)
            {
                var message = Console.ReadLine();
                using var client = TntBuilder.UseContract<IExampleContract>()
                    .CreateTcpClientConnection(IPAddress.Loopback, 12345);
                client.Contract.Send("Superman", message);
            }
        }
        finally
        {
            server.Close();
        }
    }

}

//contract
public interface IExampleContract
{
    [TntMessage(1)]
    void Send(string user, string message);
}

//contract implementation
public class ExampleContract : IExampleContract
{
    public void Send(string user, string message)
    {
        Console.WriteLine($"[Server received:] {user} : {message}");
    }
}