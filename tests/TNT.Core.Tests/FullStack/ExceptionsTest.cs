using System;
using System.Diagnostics.Contracts;
using System.Net;
using System.Threading.Tasks;
using CommonTestTools;
using CommonTestTools.Contracts;
using NUnit.Framework;
using TNT.Core.Api;
using TNT.Core.Exceptions.Local;
using TNT.Core.Exceptions.Remote;
using TNT.Core.Tcp;

namespace TNT.Core.Tests.FullStack;

[TestFixture]
public class ExceptionsTest
{
    private ServerAndClient<ITestContract, TestContractMock> _serverAndClient;

    [SetUp]
    public async Task TearUp()
    {
        _serverAndClient = await ServerAndClient<ITestContract, TestContractMock>.Create();
    }

    [TearDown]
    public void Disposing()
    {
        _serverAndClient.Dispose();
    }

    #region ConnectionIsLostException
    [Test]
    public void ProxyConnectionIsLost_SayCallThrows()
    {
        _serverAndClient.ClientSideConnection.Dispose();

        TestTools.AssertThrowsAndNotBlocks<ConnectionIsLostException>(() => _serverAndClient.ClientSideConnection.Contract.Say());
    }

    [Test]
    public async Task ProxyConnectionIsLost_AskCallThrows()
    {
        _serverAndClient.ClientSideConnection.Dispose();

        await TestTools.AssertThrowsAndNotBlocksAsync<ConnectionIsLostException>(() => _serverAndClient.ClientSideConnection.Contract.AskAsync());
    }
    #endregion

    #region RemoteUnhandledException
    [Test]
    public async Task Proxy_SayWithException_CallNotThrows()
    {
        await TestTools.AssertNotBlocks(() => _serverAndClient
        .ClientSideConnection.Contract.SayWithException());
    }
    [Test]
    public void Proxy_AskUserUnhandled_Throws()
    {
        TestTools.AssertThrowsAndNotBlocks<RemoteUnhandledException>(() => _serverAndClient
        .ClientSideConnection.Contract.AskWithException("asd"));
    }
    [Test]
    public async Task Proxy_SayAsyncUserUnhandled_Throws()
    {
        await TestTools.AssertThrowsAndNotBlocksAsync<RemoteUnhandledException>(() => _serverAndClient
        .ClientSideConnection.Contract.SayWithExceptionAsync("thjg"));
    }
    [Test]
    public async Task Proxy_AskAsyncUserUnhandled_Throws()
    {
        await TestTools.AssertThrowsAndNotBlocksAsync<RemoteUnhandledException>(() => _serverAndClient
        .ClientSideConnection.Contract.AskWithExceptionAsync());
    }
    #endregion

    [Test]
    public async Task Proxy_AskMissingCord_Throws()
    {
        TntTcpServer<IEmptyContract> server = null;
        try
        {
            server = TntBuilder
            .UseContract<IEmptyContract, EmptyContract>()
            .CreateTcpServer(IPAddress.Loopback, 12346);

            server.Start();

            var clientSide = await TntBuilder
               .UseContract<ITestContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12346);

            var serverSide = await server.WaitForAClient();

            TestTools.AssertThrowsAndNotBlocks<RemoteContractImplementationException>(() => clientSide.Contract.Ask());
        }
        finally
        {
            server?.Dispose();
        }
    }
    [Test]
    public async Task Proxy_SayMissingCord_NotThrows()
    {
        TntTcpServer<IEmptyContract> server = null;
        try
        {
            server = TntBuilder
            .UseContract<IEmptyContract, EmptyContract>()
            .CreateTcpServer(IPAddress.Loopback, 12346);

            server.Start();

            var clientSide = await TntBuilder
               .UseContract<ITestContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12346);

            var serverSide = await server.WaitForAClient();

            await TestTools.AssertNotBlocks(clientSide.Contract.Say);
        }
        finally
        {
            server?.Dispose();
        }        
    }
    [Test]
    public async Task Origin_SayMissingCord_NotThrows()
    {
        TntTcpServer<ITestContract> server = null;
        try
        {
            server = TntBuilder
            .UseContract<ITestContract, TestContractMock>()
            .CreateTcpServer(IPAddress.Loopback, 12346);

            server.Start();

            var clientSide = await TntBuilder
               .UseContract<IEmptyContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12346);

            var serverSide = await server.WaitForAClient();

            await TestTools.AssertNotBlocks(serverSide.Contract.OnSay);
        }
        finally
        {
            server?.Dispose();
        }
        
    }

    [Test]
    public async Task Origin_SayExceptioanlCallback_NotThrows()
    {
        _serverAndClient.ClientSideConnection.Contract.OnSay += () => throw new InvalidOperationException();
        _serverAndClient.ClientSideConnection.Contract.OnAsk += () => throw new InvalidOperationException();

        await TestTools.AssertNotBlocks(_serverAndClient.ServerSideConnection.Contract.OnSay);
    }
    [Test]
    public void Origin_AskExceptioanlCallback_Throws()
    {
        _serverAndClient.ClientSideConnection.Contract.OnSay += () => throw new InvalidOperationException();
        _serverAndClient.ClientSideConnection.Contract.OnAsk += () => throw new InvalidOperationException();

        TestTools.AssertThrowsAndNotBlocks<RemoteUnhandledException>(() => _serverAndClient.ServerSideConnection.Contract.OnAsk());
    }

    [Test]
    public async Task Origin_AsksNotImplemented_returnsDefault()
    {
        var answer = await TestTools.AssertNotBlocks(() => _serverAndClient.ServerSideConnection.Contract.OnAsk());
        Assert.That(default == answer);
    }
}