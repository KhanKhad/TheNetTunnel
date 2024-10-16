﻿//using NUnit.Framework;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using TNT.Core.Presentation;
//using TNT.Core.Presentation.Serializers;
//using TNT.Core.Testing;
//using TNT.Core.Transport;

//namespace Tnt.LongTests;

//[TestFixture]
//public class ConcurrentSenderTest
//{
//    [TestCase(1000000, 100)]
//    public void Sends(int length, int concurrentLevel)
//    {
//        var channel = TestChannel.CreateSingleThread();
//        channel.ImmitateConnect();

//        var transporter = new Transporter(channel);

//        int id = 1;
//        var serializer = new ByteArraySerializer();
//        var sender = new Sender(transporter, new Dictionary<int, ISerializer> { { id, serializer } });

//        int expectedHeadLength = 6;
//        var start = new ManualResetEvent(false);
//        int doneThreads = 0;
            
//        for (int i = 0; i < concurrentLevel; i++)
//        {
//            ThreadPool.QueueUserWorkItem(_ =>
//            {
//                byte[] array = CreateArray(length, (byte)i);
//                start.WaitOne();
//                sender.Say(id, new object[] { array });
//            });
//        }
//        channel.OnWrited += (_, arg) =>
//        {
//            Assert.AreEqual(expectedHeadLength + length, arg.Length);
//            byte lastValue = arg.Last();
//            for (int i = expectedHeadLength; i < expectedHeadLength + length; i++)
//            {
//                Assert.AreEqual(lastValue, arg[i]);
//            }
//            doneThreads++;
//        };

//        start.Set();

//        while (doneThreads != concurrentLevel - 1)
//        {
//            Thread.Sleep(1);
//        }
//    }

//    private static byte[] CreateArray(int length, byte value)
//    {
//        var array = new byte[length];
//        for (int i = 0; i < length; i++)
//        {
//            array[i] = value;
//        }

//        return array;
//    }
//}