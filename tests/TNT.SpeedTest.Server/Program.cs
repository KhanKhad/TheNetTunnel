using System;
using System.Net;
using TNT.Core.Api;
using TNT.SpeedTest.Contracts;
using TNT.Core.Tcp;

namespace TNT.SpeedTest.Server;

class Program
{
    static void Main(string[] args)
    {
        int port = 24731;

        var server = TntBuilder
            .UseContract<ISpeedTestContract, SpeedTestContract>()
            .UseMultiOperationDispatcher()
            .CreateTcpServer(IPAddress.Any, port);

        server.Start();
        Console.WriteLine($"Speed test server opened at port {port}");

        while (true)
        {
            Console.WriteLine("Write \"stop\" to exit");
            if (Console.ReadLine().ToLower() == "stop")
            {
                server.Dispose();
                Console.WriteLine("Server stopped");
                return;
            }
        }

    }
}