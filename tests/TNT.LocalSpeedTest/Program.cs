﻿using System;
using System.Net;
using TNT.Core.Api;
using TNT.Core.Presentation.ReceiveDispatching;
using TNT.SpeedTest;
using TNT.SpeedTest.Contracts;
using TNT.SpeedTest.OutputBandwidth;
using TNT.SpeedTest.TransactionBandwidth;
using TNT.Core.Transport;
using TNT.Core.Tcp;

namespace TNT.LocalSpeedTest;

class Program
{
    private static readonly Output _output = new Output();

    static void Main()
    {
        _output.WriteLine("Current time: "+ DateTime.Now);
        _output.WriteLine("Machine:" + System.Environment.MachineName);
        _output.WriteLine("Local measurement test started");
        _output.WriteLine();
        new ProtobuffNetClearSerialzationTest(_output).Run();
        _output.WriteLine();
        _output.WriteLine();

        TestLocalhost();
        _output.WriteLine();
        _output.WriteLine();

        TestDirectTestConnection();
        _output.WriteLine();
        _output.WriteLine("Measurements are done");
        while (true)
        {
            Console.WriteLine("Save results [y/n]?");
            var key = Console.ReadKey().Key;
            if (key == ConsoleKey.Y)
            {
                while (true)
                {
                    Console.WriteLine("Enter file name [MeasureResults.txt]:");
                    var name = Console.ReadLine();

                    if (!_output.TrySaveTo(
                            String.IsNullOrWhiteSpace(name)? "MeasureResults.txt":name))
                    {
                        Console.WriteLine("Saving failed");
                        continue;
                    }
                    Console.WriteLine("Succesfully saved");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadLine();
                    return;
                }
                  
            }
            else if(key== ConsoleKey.N)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
                return;
            }
        }
    }

    private static void TestDirectTestConnection()
    {
        _output.WriteLine("-------------Direct test mock test--------------");

        var pair = TntTestHelper.CreateThreadlessChannelPair();
        var proxy = TntBuilder
            .UseContract<ISpeedTestContract>()
            .UseReceiveDispatcher<ReceiveDispatcher>()
            .UseChannel(pair.ChannelA)
            .Build();

        var origin = TntBuilder
            .UseContract<ISpeedTestContract, SpeedTestContract>()
            .UseReceiveDispatcher<ReceiveDispatcher>()
            .UseChannel(pair.ChannelB)
            .Build();
        pair.ConnectAndStartReceiving();

        Test(proxy);

        pair.Disconnect();
    }

    private static void TestLocalhost()
    {
        _output.WriteLine("-------------Localhost test--------------");
        using var server = TntBuilder
            .UseContract<ISpeedTestContract, SpeedTestContract>()
            .UseReceiveDispatcher<ReceiveDispatcher>()
            .CreateTcpServer(IPAddress.Loopback, 12345);
        server.StartListening();

        using var client = TntBuilder
            .UseContract<ISpeedTestContract>()
            .UseReceiveDispatcher<ReceiveDispatcher>()
            .CreateTcpClientConnection(IPAddress.Loopback, 12345);
        Test(client);
    }

    private static void Test(IConnection<ISpeedTestContract, IChannel> client)
    {
        client.Contract.AskForTrue();
        client.Contract.AskBytesEcho(new byte[] { 1, 2, 3, });
        client.Contract.AskIntegersEcho(new[] { 1, 2, 3, });
        client.Contract.AskTextEcho("taram pam pam");
        client.Contract.SayBytes(new byte[] { 1, 2, 3, });
        client.Contract.SayProtoStructEcho(new ProtoStruct());
        client.Contract.AskProtoStructEcho(new ProtoStruct());

        var overheadTest = new TransactionOverheadTest(client.Contract, client.Channel, _output);
        overheadTest.MeasureOutputOverhead();
        _output.WriteLine();

        overheadTest.MeasureTransactionOverhead();
        _output.WriteLine();

        var outputTest = new OutputTestMeasurement(client.Contract, client.Channel, _output);
        outputTest.Measure();
        _output.WriteLine();
        var transactionTest = new TransactionMeasurement(client.Contract, client.Channel, _output);
        transactionTest.Measure();
           
    }
}