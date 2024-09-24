using System;
using TNT.Core.Api;

namespace TNT.Core.Testing
{
    public class TestChannelListener: IChannelListener<TestChannel>
    {
        public bool IsListening { get; set; }
        public event Action<IChannelListener<TestChannel>, TestChannel> Accepted;

        public TestChannelPair ImmitateAccept(TestChannel incomeChannel)
        {
            if(!IsListening)
                throw  new  InvalidOperationException();
            var thisChannel = TestChannel.CreateThreadSafe();
            var pair = TntTestHelper.CreateChannelPair(thisChannel, incomeChannel);
            pair.ConnectAndStartReceiving();
            Accepted?.Invoke(this, thisChannel);
            return pair;
        }
    }
}
