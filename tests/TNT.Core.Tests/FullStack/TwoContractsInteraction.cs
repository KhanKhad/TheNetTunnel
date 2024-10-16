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
    private ServerAndClient<ITestContract, ITestContract, TestContractMock> _serverAndClient;

    [SetUp]
    public async Task TearUp()
    {
        _serverAndClient = await ServerAndClient<ITestContract, ITestContract, TestContractMock>.Create();
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


    [TestCase("Hey you", 12, 24)]
    [TestCase("", 234, 0)]
    [TestCase(null, 0, long.MaxValue)]
    public void ProxyAskCall_ReturnsCorrectValue(string s, int i, long l)
    {
        var func = new Func<string, int, long, string>((s1, i2, l3) =>
        {
            return s1 + i2.ToString() + l3.ToString();
        });

        ((TestContractMock)_serverAndClient.ServerSideConnection.Contract).WhenAskSILCalledCall(func);

        

        var proxyResult = _serverAndClient.ClientSideConnection.Contract.Ask(s, i, l);
        var originResult = func(s, i, l);

        Assert.That(originResult == proxyResult);
    }
    [TestCase("Hey you")]
    [TestCase("")]
    [TestCase(null)]
    public void ProxyAskCall_ReturnsSettedValue(string returnedValue)
    {
        //set 'echo' handler
        _serverAndClient.ClientSideConnection.Contract.OnAskS += (arg) => arg;
        //call
        var proxyResult = _serverAndClient.ServerSideConnection.Contract.OnAskS(returnedValue);
        Assert.That(returnedValue == proxyResult);
    }
    [Test]
    public async Task ConveyourDispatcher_NetworkDeadlockNotHappens()
    {
        var val = 0;
        _serverAndClient.ClientSideConnection.Contract.OnAsk +=() => val = _serverAndClient.ClientSideConnection.Contract.Ask();

        var rRes = await TestTools.AssertNotBlocks(_serverAndClient.ServerSideConnection.Contract.OnAsk);
        Assert.That(TestContractMock.AskReturns == rRes);
        Assert.That(TestContractMock.AskReturns == val);
    }
}