using EX_2.Stage2_ComplexExample;
using System;
using System.Net;
using TNT;
using TNT.Core.Api;
using TNT.Core.Contract;
using TNT.Core.Presentation.ReceiveDispatching;
using TNT.Core.Testing;
using TNT.Core.Tcp;

namespace EX_2.Stage3_IntroducingToTestingExample;

public class Stage3_Example
{
    public void Run()
    {
        Console.WriteLine("TNT unit/integration test example.");
        Console.WriteLine("In this example, we will create connection with custom mock channell instead of using tcp.");
        Console.WriteLine("The example shows you how to write integration/unit tests at your code");
        Console.WriteLine();

        #region arrange

        var server = TntBuilder
            .UseContract<IStage3EchoContract, Stage3EchoContract>()
            .CreateTcpServer(IPAddress.Loopback, 12345);

        server.Start();

        var clientConnection = TntBuilder
            .UseContract<IStage3EchoContract>()
            .CreateTcpClientConnection(IPAddress.Loopback, 12345);


        #endregion

        #region act
        string testMessage = "Watup buddy?";

        var echo = clientConnection.Contract.Send("superman", testMessage);
        #endregion

        #region assert

        // use
        // Assert.AreEqual(echo, testMessage) 
        // with your test framework instead

        if (echo != testMessage)
        {
            throw new Exception("Unit test failed");
        }
        else
        {
            Console.WriteLine("Integration test passed");
        }

        #endregion

        Console.WriteLine("Press any key for exit...");
        Console.ReadKey();
    }
}
/// <summary>
/// Interface (contract) for client server interaction
/// </summary>
public interface IStage3EchoContract
{
    [TntMessage(1)] 
    string Send(string user, string message);
}
/// <summary>
/// Server implementation of the interaction contract
/// </summary>
public class Stage3EchoContract : IStage3EchoContract
{
    public string Send(string user, string message)
    {
        return message;
    }
}