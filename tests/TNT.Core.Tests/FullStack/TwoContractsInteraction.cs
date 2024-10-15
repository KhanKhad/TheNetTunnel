using System;
using System.Linq;
using System.Threading.Tasks;
using CommonTestTools;
using CommonTestTools.Contracts;
using NUnit.Framework;
using TNT.Core.Api;
using TNT.Core.ReceiveDispatching;

namespace TNT.Core.Tests.FullStack;

[TestFixture]
public class TwoContractsInteraction
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

    [TestCase("Hey you")]
    [TestCase("")]
    [TestCase(null)]
    public async Task ProxySayCall_NoThreadDispatcher_OriginSayCalled(string sentMessage)
    {
        _serverAndClient.ClientSideConnection.Contract.Say(sentMessage);

        await Task.Delay(300);

        var received = ((TestContractMock)_serverAndClient.ServerSideConnection.Contract).SaySCalled.Single();

        Assert.That(sentMessage == received);
    }


    //[TestCase("Hey you",12,24)]
    //[TestCase("",234,0)]
    //[TestCase(null,0,long.MaxValue)]
    //public void ProxyAskCall_ReturnsCorrectValue(string s, int i, long l)
    //{
    //    var func = new Func<string,int,long,string>((s1, i2, l3) =>
    //    {
    //        return s1 + i2.ToString() + l3.ToString();
    //    });


    //    var channelPair = TntTestHelper.CreateThreadSafeChannelPair();

    //    var proxyConnection = TntBuilder
    //        .UseContract<ITestContract>()
    //        .UseReceiveDispatcher<ReceiveDispatcher>()
    //        .UseChannel(channelPair.ChannelA)
    //        .Build();

    //    var originConnection = TntBuilder
    //        .UseContract<ITestContract, TestContractMock>()
    //        .UseContractInitalization((c,_)=> ((TestContractMock)c).WhenAskSILCalledCall(func))
    //        .UseReceiveDispatcher<ReceiveDispatcher>()
    //        .UseChannel(channelPair.ChannelB)
    //        .Build();

    //    channelPair.ConnectAndStartReceiving();

    //    var proxyResult = proxyConnection.Contract.Ask(s,i,l);
    //    var originResult = func(s, i, l);
    //    Assert.AreEqual(originResult, proxyResult);
    //}
    //[TestCase("Hey you")]
    //[TestCase("")]
    //[TestCase(null)]
    //public void ProxyAskCall_ReturnsSettedValue(string returnedValue)
    //{

    //    var channelPair = TntTestHelper.CreateThreadSafeChannelPair();

    //    var proxyConnection = TntBuilder
    //        .UseContract<ITestContract>()
    //        .UseReceiveDispatcher<ReceiveDispatcher>()
    //        .UseChannel(channelPair.ChannelA)
    //        .Build();

    //    var originConnection = TntBuilder
    //        .UseContract<ITestContract, TestContractMock>()
    //        .UseContractInitalization((c, _) => c.OnAskS += (arg)=>arg)
    //        .UseReceiveDispatcher<ReceiveDispatcher>()
    //        .UseChannel(channelPair.ChannelB)
    //        .Build();

    //    channelPair.ConnectAndStartReceiving();
    //    //set 'echo' handler
    //    proxyConnection.Contract.OnAskS += (arg) => arg;
    //    //call
    //    var proxyResult = originConnection.Contract.OnAskS(returnedValue);
    //    Assert.AreEqual(returnedValue, proxyResult);
    //}
    //[Test]
    //public void ConveyourDispatcher_NetworkDeadlockNotHappens()
    //{
    //    var channelPair = TntTestHelper.CreateThreadSafeChannelPair();
    //    var proxyConnection = TntBuilder
    //        .UseContract<ITestContract>()
    //        //On income ask request, calling rpc ask. 
    //        //It can provoke an networkDeadlock 
    //        .UseContractInitalization((c,_)=> c.OnAsk+=c.Ask)
    //        .UseChannel(channelPair.ChannelA)
    //        .Build();

    //    var originConnection = TntBuilder
    //        .UseContract<ITestContract, TestContractMock>()
    //        .UseChannel(channelPair.ChannelB)
    //        .Build();

    //    channelPair.ConnectAndStartReceiving();

    //    var task = TestTools.AssertNotBlocks(originConnection.Contract.OnAsk);
    //    Assert.AreEqual(TestContractMock.AskReturns, task.Result);
    //}
}