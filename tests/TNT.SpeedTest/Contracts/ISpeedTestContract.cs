

using System;
using TNT.Core.Contract;

namespace TNT.SpeedTest.Contracts;

public interface ISpeedTestContract
{
    [TntMessageAttribute(1)]  byte[] AskBytesEcho(byte[] data);
    [TntMessageAttribute(2)]  int[] AskIntegersEcho(int[] data);
    [TntMessageAttribute(3)]  string AskTextEcho(string data);
    [TntMessageAttribute(4)] ProtoStruct AskProtoStructEcho(ProtoStruct data);
    [TntMessageAttribute(5)] void SayNothing();
    [TntMessageAttribute(6)] void SayBytes(byte[] data);
    [TntMessageAttribute(7)] void SayProtoStructEcho(ProtoStruct data);
    [TntMessageAttribute(8)] void SayString(string data);
    [TntMessageAttribute(9)] bool AskForTrue();
    [TntMessageAttribute(10)] void SubscribeForSayCalled(int sayCalldTimes);
    [TntMessageAttribute(11)] Action SaysCallsCountReceived { get; set; }
}