namespace TNT.Core.Testing
{
    public class TestChannelPair
    {
        public TestChannelPair(TestChannel cahnnelA, TestChannel channelB)
        {
            ChannelA = cahnnelA;
            ChannelB = channelB;
            FromAToB = new OneSideConnection(ChannelA, channelB);
            FromBToA = new OneSideConnection(ChannelB, cahnnelA);
        }

        private OneSideConnection FromAToB;
        private OneSideConnection FromBToA;
        
        public TestChannel ChannelA { get; }
        public TestChannel ChannelB { get; }

        public bool IsConnected { get; private set; }

        public void Connect()
        {
            ChannelA.ImmitateConnect();
            ChannelB.ImmitateConnect();
            FromAToB.Start();
            FromBToA.Start();
            IsConnected = true;
        }

        public void ConnectAndStartReceiving()
        {
            Connect();
            ChannelB.AllowReceive = true;
            ChannelA.AllowReceive = true;
        }

        public void Disconnect()
        {
            FromAToB.Stop();
            FromBToA.Stop();
            ChannelB.ImmitateDisconnect();
            ChannelA.ImmitateDisconnect();
            IsConnected = false;

        }
    }
}