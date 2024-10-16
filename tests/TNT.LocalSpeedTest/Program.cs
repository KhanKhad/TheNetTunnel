using System;
using System.Net;
using TNT.Core.Api;
using TNT.SpeedTest;
using TNT.SpeedTest.Contracts;
using TNT.SpeedTest.OutputBandwidth;
using TNT.SpeedTest.TransactionBandwidth;
using TNT.Core.Transport;
using TNT.Core.Tcp;
using CommonTestTools.Contracts;
using CommonTestTools;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;

namespace TNT.LocalSpeedTest;

class Program
{
    private static readonly Output _output = new Output();

    static async Task Main()
    {
        _output.WriteLine("Current time: "+ DateTime.Now);
        _output.WriteLine("Machine:" + System.Environment.MachineName);
        _output.WriteLine("Local measurement test started");
        _output.WriteLine();
        new ProtobuffNetClearSerialzationTest(_output).Run();
        _output.WriteLine();
        _output.WriteLine();

        await TestLocalhost();
        _output.WriteLine();
        _output.WriteLine();

        await TestDirectTestConnection();
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

    private static async Task TestDirectTestConnection()
    {
        _output.WriteLine("-------------Direct test mock test--------------");

        var server = TntBuilder
            .UseContract<ISpeedTestContract, SpeedTestContract>()
            .CreateTcpServer(IPAddress.Loopback, 12345);

        try
        {
            server.Start();

            var clientSide = await TntBuilder
               .UseContract<ISpeedTestContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

            var serverSide = await server.WaitForAClient();

            Test(clientSide);
        }
        finally
        {
            server.Dispose();
        }
    }

    private static async Task TestLocalhost()
    {
        _output.WriteLine("-------------Localhost test--------------");

        var server = TntBuilder
            .UseContract<ISpeedTestContract, SpeedTestContract>()
            .CreateTcpServer(IPAddress.Loopback, 12345);

        try
        {
            server.Start();

            var clientSide = await TntBuilder
               .UseContract<ISpeedTestContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

            var serverSide = await server.WaitForAClient();

            Test(clientSide);
        }
        finally
        {
            server.Dispose();
        }        
    }

    private static void Test(IConnection<ISpeedTestContract> client)
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