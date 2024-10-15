using System.Linq;
using CommonTestTools;
using System.Net;
using CommonTestTools.Contracts;
using NUnit.Framework;
using TNT.Core.Api;
using TNT.Core.Tcp;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;

namespace TNT.Core.Tests.FullStack;

[TestFixture]
public class ServerTest
{
    [Test]
    public async Task ServerAcceptConnection_BeforeConnectRaised()
    {
        TntTcpServer<ITestContract> server = null;
        try
        {
            server = TntBuilder
            .UseContract<ITestContract, TestContractMock>()
            .CreateTcpServer(IPAddress.Loopback, 12346);

            BeforeConnectEventArgs<ITestContract> args = null;
            server.BeforeConnect += (a, b) => args = b;

            server.Start();

            var clientSide = await TntBuilder
               .UseContract<ITestContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12346);

            var serverSide = await server.WaitForAClient();

            Assert.That(args, Is.Not.Null, "BeforeConnect is not raised");
        }
        finally
        {
            server?.Dispose();
        }
    }

    [Test]
    public async Task ServerAcceptConnection_AfterConnectRaised()
    {
        TntTcpServer<ITestContract> server = null;
        try
        {
            server = TntBuilder
            .UseContract<ITestContract, TestContractMock>()
            .CreateTcpServer(IPAddress.Loopback, 12346);

            IConnection<ITestContract> args = null;
            server.AfterConnect += (a, b) => args = b;

            server.Start();

            var clientSide = await TntBuilder
               .UseContract<ITestContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12346);

            var serverSide = await server.WaitForAClient();

            Assert.That(args, Is.Not.Null, "AfterConnect is not raised");
        }
        finally
        {
            server?.Dispose();
        }
    }


    [Test]
    public async Task ServerAcceptConnection_AllowReceiveEqualTrue()
    {
        ServerAndClient<ITestContract, TestContractMock> serverAndClient = null;
        try
        {
            serverAndClient = await ServerAndClient<ITestContract, TestContractMock>.Create();
            Assert.That(serverAndClient.ClientSideConnection.Channel.IsConnected, Is.True);
            Assert.That(serverAndClient.ServerSideConnection.Channel.IsConnected, Is.True);
        }
        finally
        {
            serverAndClient?.Dispose();
        }
    }

    [Test]
    public async Task ClientDisconnected_DisconnectedRaised()
    {
        ServerAndClient<ITestContract, TestContractMock> serverAndClient = null;
        try
        {
            serverAndClient = await ServerAndClient<ITestContract, TestContractMock>.Create();

            var clientDisconnectEventRaised = false;
            var serverDisconnectEventRaised = false;

            serverAndClient.ClientSideConnection.Channel.OnDisconnect += (a, b) => clientDisconnectEventRaised = true;
            serverAndClient.ServerSideConnection.Channel.OnDisconnect += (a, b) => serverDisconnectEventRaised = true;

            serverAndClient.ClientSideConnection.Dispose();

            Assert.That(clientDisconnectEventRaised, "Disconnect not raised");

            serverAndClient.ServerSideConnection.Dispose();

            Assert.That(serverDisconnectEventRaised, "Disconnect not raised");
        }
        finally
        {
            serverAndClient?.Dispose();
        }
    }
}