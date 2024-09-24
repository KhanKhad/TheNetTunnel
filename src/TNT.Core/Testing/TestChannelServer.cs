using TNT.Core.Api;

namespace TNT.Core.Testing
{
    public class TestChannelServer<TContract>: ChannelServer<TContract,TestChannel> 
        where TContract : class
    {
        public TestChannelListener TestListener { get;  }
        public TestChannelServer(PresentationBuilder<TContract> channelBuilder) : base(channelBuilder, new TestChannelListener())
        {
            TestListener = this.Listener as TestChannelListener;
        }
    }
}
