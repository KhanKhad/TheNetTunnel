namespace TNT.Core.Testing
{
    public static class TntTestHelper
    {
        public static TestChannelPair CreateThreadSafeChannelPair()
        {
            return  new TestChannelPair(TestChannel.CreateThreadSafe(), TestChannel.CreateThreadSafe());
        }
        public static TestChannelPair CreateThreadlessChannelPair()
        {
            return new TestChannelPair(TestChannel.CreateSingleThread(), TestChannel.CreateSingleThread());
        }
        public static TestChannelPair CreateChannelPair(TestChannel cahnnelA, TestChannel channelB)
        {
            return  new TestChannelPair(cahnnelA, channelB);
        }
    }
}