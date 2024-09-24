using CommonTestTools.Contracts;
using NUnit.Framework;
using TNT.Core.Api;
using TNT.Core.Exceptions.ContractImplementation;
using TNT.Core.Testing;

namespace TNT.Core.Tests.FullStack;

[TestFixture]
public class ConnectionBuilderTest
{
    [Test]
    public void ProxyBuilder_ChannelConnectedBefore_SayCalled_DataSent()
    {
        var channel = TestChannel.CreateThreadSafe();
        channel.ImmitateConnect();

        var proxyConnection = TntBuilder
            .UseContract<ITestContract>()
            .UseChannel(channel)
            .Build();

        byte[] sentMessage = null;
        proxyConnection.Channel.OnWrited += (s, msg) => sentMessage = msg;
        proxyConnection.Contract.Say();

        Assert.IsNotNull(sentMessage);
        Assert.IsNotEmpty(sentMessage);
    }
    [Test]
    public void ProxyBuilder_SayCalled_DataSent()
    {
        var channel = TestChannel.CreateThreadSafe();
        var proxyConnection = TntBuilder
            .UseContract<ITestContract>()
            .UseChannel(channel)
            .Build();
        proxyConnection.Channel.ImmitateConnect();
        byte[] sentMessage = null;
        proxyConnection.Channel.OnWrited += (s, msg) => sentMessage = msg;
        proxyConnection.Contract.Say();

        Assert.IsNotNull(sentMessage);
        Assert.IsNotEmpty(sentMessage);
    }
    [Test]
    public void ProxyBuilderCreatesWithCorrectConnection()
    {
        var channel = TestChannel.CreateThreadSafe();
        var proxyConnection = TntBuilder
            .UseContract<ITestContract>()
            .UseChannel(channel)
            .Build();
        Assert.AreEqual(channel, proxyConnection.Channel);
    }
    [Test]
    public void ProxyBuilderBuilds_ChannelAllowReceiveIsTrue()
    {
        var channel = TestChannel.CreateThreadSafe();
        channel.ImmitateConnect();
        var proxyConnection = TntBuilder
            .UseContract<ITestContract>()
            .UseChannel(channel)
            .Build();

        Assert.IsTrue(channel.AllowReceive);
    }
    [Test]

    public void ProxyBuilder_UseContractInitalization_CalledBeforeBuildDone()
    {
        var channel = TestChannel.CreateThreadSafe();
        ITestContract initializationArgument = null;
        var proxyConnection = TntBuilder.UseContract<ITestContract>()
            .UseContractInitalization((i,c)=> initializationArgument = i)
            .UseChannel(channel)
            .Build();
        Assert.IsNotNull(initializationArgument);
        Assert.AreEqual(proxyConnection.Contract, initializationArgument);
    }
    [Test]

    public void ConnectionDisposes_channelBecomesDisconnected()
    {
        var channel = TestChannel.CreateThreadSafe();
        using (var proxyConnection = TntBuilder.UseContract<ITestContract>()
                   .UseChannel(channel)
                   .Build())
        {
            proxyConnection.Channel.ImmitateConnect();    
        }
        Assert.IsFalse(channel.IsConnected);
    }
    [Test]
    public void OriginContract_CreatesByType_ContractCreated()
    {
        var channel = TestChannel.CreateThreadSafe();
            
        var proxyConnection = TntBuilder
            .UseContract<ITestContract, TestContractMock>()
            .UseChannel(channel)
            .Build();
        Assert.IsNotNull(proxyConnection.Contract);
    }
    [Test]
    public void OriginContract_CreatesByFactory_ContractCreated()
    {
        var channel = TestChannel.CreateThreadSafe();

        var proxyConnection = TntBuilder
            .UseContract<ITestContract, TestContractMock>()
            .UseChannel(channel)
            .Build();

        Assert.IsNotNull(proxyConnection.Contract);
    }
    [Test]
    public void OriginContractAsInterface_CreatesByFactory_ContractCreated()
    {
        var channel = TestChannel.CreateThreadSafe();

        var proxyConnection = TntBuilder
            .UseContract<ITestContract, TestContractMock>()
            .UseChannel(channel)
            .Build();

        Assert.IsInstanceOf<ITestContract>(proxyConnection.Contract);
    }

    [Test]
    public void OriginContractAsSingleTone_CreatesByFactory_ContractCreated()
    {
        var channel = TestChannel.CreateThreadSafe();

        var contract = new TestContractMock();
        var proxyConnection = TntBuilder
            .UseContract<ITestContract>(contract)
            .UseChannel(channel)
            .Build();
        Assert.AreEqual(contract, proxyConnection.Contract);
    }

    [Test]
    public void UnserializeableContract_CreateT_throwsException()
    {
        var channel = TestChannel.CreateThreadSafe();

        var builder = TntBuilder
            .UseContract<IUnserializeableContract>()
            .UseChannel(channel);
         
        Assert.Throws<TypeCannotBeSerializedException>(()=>builder.Build());
    }
    [Test]
    public void UnDeserializeableContract_CreateT_throwsException()
    {
        var channel = TestChannel.CreateThreadSafe();

        var builder = TntBuilder
            .UseContract<IUnDeserializeableContract>()
            .UseChannel(channel);

        Assert.Throws<TypeCannotBeDeserializedException>(() => builder.Build());
    }
}