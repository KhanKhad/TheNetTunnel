using System;
using System.Net;
using TNT.Core.Api;
using TNT.Core.Presentation.ReceiveDispatching;
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
            .UseReceiveDispatcher<ConveyorDispatcher>()
            .CreateTcpServer(IPAddress.Any, port);

        server.StartListening();
        Console.WriteLine($"Speed test server opened at port {port}");

        while (true)
        {
            Console.WriteLine("Write \"stop\" to exit");
            if (Console.ReadLine().ToLower() == "stop")
            {
                server.Close();
                Console.WriteLine("Server stopped");
                return;
            }
        }

    }
}