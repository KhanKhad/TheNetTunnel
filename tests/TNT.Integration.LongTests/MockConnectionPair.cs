using System;
using System.Linq;
using CommonTestTools;
using NUnit.Framework;
using TNT;
using TNT.Core.Api;
using TNT.Core.Testing;

namespace Tnt.LongTests;

public class MockConnectionPair<TProxyContractInterface, TOriginContractInterface, TOriginContractType> : IDisposable
    where TProxyContractInterface: class
    where TOriginContractInterface: class 
    where TOriginContractType: class, TOriginContractInterface,  new() 
{
    public IConnection<TProxyContractInterface, TestChannel> ProxyConnection { get; }
    public TestChannel ClientChannel { get; }
    private EventAwaiter<IConnection<TOriginContractInterface, TestChannel>> _eventAwaiter;

    public IConnection<TOriginContractInterface, TestChannel> OriginConnection { get; private set; } = null;
    public TOriginContractType OriginContract => OriginConnection.Contract as TOriginContractType;
    public TProxyContractInterface ProxyContract => ProxyConnection.Contract;
    public TestChannelServer<TOriginContractInterface> Server { get; }

    public MockConnectionPair(ContractBuilder<TOriginContractInterface> originBuilder,
        ContractBuilder<TProxyContractInterface> proxyBuider, bool connect = true)
    {
        ClientChannel = TestChannel.CreateThreadSafe();

        Server = new TestChannelServer<TOriginContractInterface>(originBuilder);
        ProxyConnection = proxyBuider.UseChannel(ClientChannel).Build();
        if (connect)
            Connect();
    }
    
    public MockConnectionPair(bool connect = true)
    {
        var serverBuilder = TntBuilder
            .UseContract<TOriginContractInterface, TOriginContractType>()
            //  .UseReceiveDispatcher<NotThreadDispatcher>()
            .SetMaxAnsTimeout(200000);

        ClientChannel = TestChannel.CreateThreadSafe();
        var clientBuilder = TntBuilder
            .UseContract<TProxyContractInterface>()
            // .UseReceiveDispatcher<NotThreadDispatcher>()
            .SetMaxAnsTimeout(200000);
        Server = new TestChannelServer<TOriginContractInterface>(serverBuilder);
        ProxyConnection = clientBuilder.UseChannel(ClientChannel).Build();

        if (connect)
            Connect();
    }
    
    public void Connect()
    {
        _eventAwaiter = new EventAwaiter<IConnection<TOriginContractInterface, TestChannel>>();
        Server.AfterConnect += _eventAwaiter.EventRaised;
        Server.StartListening();
        Server.TestListener.ImmitateAccept(ClientChannel);
        OriginConnection = _eventAwaiter.WaitOneOrDefault(500);
        Assert.IsNotNull(OriginConnection);
    }

    public void Disconnect()
    {
        OriginConnection.Channel.Disconnect();
        ProxyConnection.Channel.Disconnect();
    }

    public void DisconnectAndClose()
    {
        Disconnect();
        Server.Close();
    }
    
    public void Dispose()
    {
        DisconnectAndClose();
    }
    
    public void AssertPairIsConnected()
    {
        Assert.IsTrue( ProxyConnection.Channel.IsConnected, "Client connection has to be connected");
        Assert.IsTrue(OriginConnection.Channel.IsConnected, "Server connection has to be connected");
        Assert.AreEqual(1, Server.GetAllConnections().Count(), "Server connections list has to have 1 connection");
        Assert.IsTrue(Server.IsListening, "Server has to continue listening");
    }
    
    public void AssertPairIsDisconnected()
    {
        //Some disconnection subroutine could be asynchronous.
        TestTools.AssertTrue(() => !ProxyConnection.Channel.IsConnected, 1000, "Client connection has to be disconnected");
        TestTools.AssertTrue(() => !OriginConnection.Channel.IsConnected, 1000, "Server connection has to be disconnected");
        TestTools.AssertTrue(() => !Server.GetAllConnections().Any(), 1000, "Server connections list has to be empty");
        TestTools.AssertTrue(() =>  Server.IsListening, 1000, "Server has to continue listening");
    }
}