using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommonTestTools;
using NUnit.Framework;
using TNT.Core.Api;
using TNT.Core.Presentation;
using TNT.Core.Tcp;

namespace TNT.IntegrationTests.Serialization;

[TestFixture]
public class BigProtobuffConcurrentCallTest
{
    [TestCase(2)]
    [TestCase(10)]
    [TestCase(100)]
    [TestCase(500)]
    [TestCase(800)]
    [TestCase(900)]
    [TestCase(990)]
    [TestCase(1024)]
    [TestCase(2 * 1024)]
    [TestCase(ushort.MaxValue + 1)]
    [TestCase(ushort.MaxValue * 2 - 1)]
    [TestCase(ushort.MaxValue * 2)]
    public async Task StringPackets_transmitsViaTcp_Concurrent(int stringLengthInBytes)
    {
        var serverAndClient = await ServerAndClient<ISingleMessageContract<string>, ISingleMessageContract<string>, SingleMessageContract<string>>.Create();

        try
        {
            //Tasks count:
            int sentCount = 10;

            string originStringArgument = generateRandomString(stringLengthInBytes / 2);

            var receivedList = new ConcurrentBag<string>();

            ((SingleMessageContract<string>)serverAndClient.ServerSideConnection.Contract).SayCalled += (Sender, received) => receivedList.Add(received);

            //start monitoring disconnection events. 
            var serverDisconnectAwaiter
                = new EventAwaiter<ErrorMessage>();

            serverAndClient.ServerSideConnection.Channel.OnDisconnect += serverDisconnectAwaiter.EventRaised;

            var clientDisconnectAwaiter
                = new EventAwaiter<ErrorMessage>();

            serverAndClient.ClientSideConnection.Channel.OnDisconnect += serverDisconnectAwaiter.EventRaised;

            #region sending

            var sentTasks = new List<Task>(sentCount);

            for (var i = 0; i < sentCount; i++)
            {
                sentTasks.Add(Task.Run(() => serverAndClient.ServerSideConnection.Contract.Ask(originStringArgument)));
            }

            #endregion


            #region checking for exceptions

            bool allDoneSucc = false;
            Exception doneException = null;
            try
            {
                await Task.WhenAll(sentTasks);
                allDoneSucc = true;
            }
            catch (Exception e)
            {
                doneException = e;
            }

            //Check for client disconnection
            var clientDisconnectArgs = clientDisconnectAwaiter.WaitOneOrDefault(500);

            //Check for server disconnection
            var serverDisconnectedArg = serverDisconnectAwaiter.WaitOneOrDefault(500);
            if (clientDisconnectArgs != null || serverDisconnectedArg != null)
            {
                if (clientDisconnectArgs != null)
                    Assert.Fail("Client disconnected. Reason: " + clientDisconnectArgs);
                else if (serverDisconnectedArg != null)
                    Assert.Fail("Server disconnected. Reason: " + serverDisconnectedArg.Exception.Message);
            }

            //check for tasks agregate exception
            if (doneException != null)
                Assert.Fail("Client side thrown exception during the interaction: " + doneException.ToString());
            //Check for timeout
            if (!allDoneSucc)
                Assert.Fail("Test timeout ");

            #endregion

            #region checking for  serialization results

            Assert.That(sentCount == receivedList.Count);
            foreach (var received in receivedList)
            {
                Assert.That(originStringArgument == received);
            }

            #endregion
        }
        finally
        {
            serverAndClient.Dispose();
        }

    }

    [TestCase(1, 40)]
    [TestCase(10, 40)]
    [TestCase(20, 40)]
    [TestCase(50, 40)]
    [TestCase(80, 40)]
    [TestCase(100, 40)]
    [TestCase(200, 40)]
    [TestCase(400, 40)]
    [TestCase(1000, 40)]
    public async Task ProtobuffPackets_transmitViaTcp_Concurrent(int sizeOfCompanyInUsers, int parralelTasksCount)
    {
        //1000 = 0.5mb  
        //2000 = 2mb    
        //5000 = 10mb   
        //10000 = 50mb  5

        var serverAndClient = await ServerAndClient<ISingleMessageContract<Company>, ISingleMessageContract<Company>, SingleMessageContract<Company>>.Create();

        try
        {
            var originCompany = IntegrationTestsHelper.CreateCompany(sizeOfCompanyInUsers);
            var receivedList = new ConcurrentBag<Company>();

            ((SingleMessageContract<Company>)serverAndClient.ServerSideConnection.Contract).SayCalled += (Sender, received) => receivedList.Add(received);

            //start monitoring disconnection events. 
            var serverDisconnectAwaiter
                = new EventAwaiter<ErrorMessage>();

            serverAndClient.ServerSideConnection.Channel.OnDisconnect += serverDisconnectAwaiter.EventRaised;

            var clientDisconnectAwaiter
                = new EventAwaiter<ErrorMessage>();

            serverAndClient.ClientSideConnection.Channel.OnDisconnect += serverDisconnectAwaiter.EventRaised;

            #region sending

            var sentTasks = new List<Task>(parralelTasksCount);
            for (var i = 0; i < parralelTasksCount; i++)
                sentTasks.Add(new Task(() => serverAndClient.ClientSideConnection.Contract.Ask(originCompany)));
            foreach (var task in sentTasks)
                task.Start();

            #endregion

            #region checking for exceptions

            bool allDoneSucc = false;
            Exception doneException = null;
            try
            {
                allDoneSucc = Task.WaitAll(sentTasks.ToArray(), 60000);
            }
            catch (Exception e)
            {
                doneException = e;
            }

            //Check for client disconnection
            var clientDisconnectArgs = clientDisconnectAwaiter.WaitOneOrDefault(500);

            //Check for server disconnection
            var serverDisconnectedArg = serverDisconnectAwaiter.WaitOneOrDefault(500);
            if (clientDisconnectArgs != null || serverDisconnectedArg != null)
            {
                if (clientDisconnectArgs != null)
                    Assert.Fail("Client disconnected. Reason: " + clientDisconnectArgs);
                else if (serverDisconnectedArg != null)
                    Assert.Fail("Server disconnected. Reason: " + serverDisconnectedArg);
            }

            //check for tasks agregate exception
            if (doneException != null)
                Assert.Fail("Client side thrown exception during the interaction: " + doneException.ToString());
            //Check for timeout
            if (!allDoneSucc)
                Assert.Fail("Test timeout ");

            #endregion


            #region checking for  serialization results

            Assert.That(parralelTasksCount == receivedList.Count);
            foreach (var received in receivedList)
            {
                received.AssertIsSameTo(originCompany);
            }

            #endregion
        }
        finally
        {
            serverAndClient.Dispose();
        }
    }

    [Test]
    public async Task HundredOf2mbPacket_transmitsViaTcp_oneByOne()
    {
        var serverAndClient = await ServerAndClient<ISingleMessageContract<Company>, ISingleMessageContract<Company>, SingleMessageContract<Company>>.Create();

        try
        {
            var receivedList = new ConcurrentBag<Company>();

            ((SingleMessageContract<Company>)serverAndClient.ServerSideConnection.Contract).SayCalled += (Sender, received) => receivedList.Add(received);

            var company = IntegrationTestsHelper.CreateCompany(2000);
            int sendCount = 100;
            for (int i = 0; i < sendCount; i++)
                serverAndClient.ClientSideConnection.Contract.Ask(company);
            Assert.That(sendCount == receivedList.Count);
            foreach (var received in receivedList)
            {
                received.AssertIsSameTo(company);
            }
        }
        finally
        {
            serverAndClient.Dispose();
        }
    }

    [Test]
    public async Task HundredOf2mbPacket_transmitsViaTcp_Concurrent()
    {
        var serverAndClient = await ServerAndClient<ISingleMessageContract<Company>, ISingleMessageContract<Company>, SingleMessageContract<Company>>.Create();

        try
        {
            var receivedList = new ConcurrentBag<Company>();

            ((SingleMessageContract<Company>)serverAndClient.ServerSideConnection.Contract).SayCalled += (Sender, received) => receivedList.Add(received);

            var company = IntegrationTestsHelper.CreateCompany(2000);
            int sendCount = 100;

            List<Task> sendTasks = new List<Task>(sendCount);
            for (int i = 0; i < sendCount; i++)
                sendTasks.Add(new Task(() => serverAndClient.ClientSideConnection.Contract.Ask(company)));

            foreach (Task task in sendTasks)
            {
                task.Start();
            }

            if (!Task.WaitAll(sendTasks.ToArray(), 5 * 60 * 1000))
            {
                Assert.Fail("Test timeout ");
            }


            Assert.That(sendCount == receivedList.Count);
            foreach (var received in receivedList)
            {
                received.AssertIsSameTo(company);
            }
        }
        finally
        {
            serverAndClient.Dispose();
        }
    }


    public String generateRandomString(int length)
    {
        Random random = new Random(DateTime.Now.Millisecond);
        //Initiate objects & vars    Random random = new Random();
        String randomString = "";
        int randNumber;

        //Loop ‘length’ times to generate a random number or character
        for (int i = 0; i < length; i++)
        {
            if (random.Next(1, 3) == 1)
                randNumber = random.Next(97, 123); //char {a-z}
            else
                randNumber = random.Next(48, 58); //int {0-9}

            //append random char or digit to random string
            randomString = randomString + (char)randNumber;
        }

        //return the random string
        return randomString;
    }
}