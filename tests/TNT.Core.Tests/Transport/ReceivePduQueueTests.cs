using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Transport;

namespace TheNetTunnel.Tests.Transport
{
    [TestFixture]
    public class ReceivePduQueueTests
    {
        private static byte[] Frame(byte[] payload)
        {
            var result = new byte[sizeof(int) + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(result, payload.Length);
            payload.CopyTo(result, sizeof(int));
            return result;
        }

        private static byte[] Payload(int length, byte seed = 1)
        {
            var payload = new byte[length];
            for (var i = 0; i < length; i++)
                payload[i] = (byte)(seed + i);
            return payload;
        }

        private static byte[] ReadAll(Stream stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        [Test]
        public void CompleteFrame_IsDequeuedWithSamePayload()
        {
            var queue = new ReceivePduQueue();
            var payload = Payload(100);

            queue.Enqueue(Frame(payload));

            var message = queue.DequeueOrNull();
            Assert.That(message, Is.Not.Null);
            Assert.That(ReadAll(message), Is.EqualTo(payload));
            Assert.That(queue.DequeueOrNull(), Is.Null);
        }

        [Test]
        public void TwoFramesInOneChunk_BothAreDequeuedInOrder()
        {
            var queue = new ReceivePduQueue();
            var first = Payload(10, seed: 1);
            var second = Payload(20, seed: 100);

            queue.Enqueue(Frame(first).Concat(Frame(second)).ToArray());

            Assert.That(ReadAll(queue.DequeueOrNull()), Is.EqualTo(first));
            Assert.That(ReadAll(queue.DequeueOrNull()), Is.EqualTo(second));
            Assert.That(queue.DequeueOrNull(), Is.Null);
        }

        [Test]
        public void FrameFedByteByByte_IsReassembled()
        {
            var queue = new ReceivePduQueue();
            var payload = Payload(50);
            var frame = Frame(payload);

            foreach (var b in frame)
                queue.Enqueue(new[] { b });

            Assert.That(ReadAll(queue.DequeueOrNull()), Is.EqualTo(payload));
        }

        [Test]
        public void HeaderSplitAcrossChunks_IsReassembled()
        {
            var queue = new ReceivePduQueue();
            var payload = Payload(8);
            var frame = Frame(payload);

            // 2 bytes of the length header, then the rest.
            queue.Enqueue(frame.Take(2).ToArray());
            Assert.That(queue.DequeueOrNull(), Is.Null);

            queue.Enqueue(frame.Skip(2).ToArray());
            Assert.That(ReadAll(queue.DequeueOrNull()), Is.EqualTo(payload));
        }

        [Test]
        public void ChunkCrossingFrameBoundary_ProducesBothFrames()
        {
            var queue = new ReceivePduQueue();
            var first = Payload(30, seed: 7);
            var second = Payload(15, seed: 200);
            var bytes = Frame(first).Concat(Frame(second)).ToArray();

            // Split in the middle of the second frame's header.
            var splitAt = sizeof(int) + first.Length + 2;
            queue.Enqueue(bytes.Take(splitAt).ToArray());

            Assert.That(ReadAll(queue.DequeueOrNull()), Is.EqualTo(first));
            Assert.That(queue.DequeueOrNull(), Is.Null, "Second frame must not be ready yet");

            queue.Enqueue(bytes.Skip(splitAt).ToArray());
            Assert.That(ReadAll(queue.DequeueOrNull()), Is.EqualTo(second));
        }

        [Test]
        public void ZeroLengthFrame_ProducesEmptyPacket()
        {
            var queue = new ReceivePduQueue();

            queue.Enqueue(Frame(Array.Empty<byte>()));

            var message = queue.DequeueOrNull();
            Assert.That(message, Is.Not.Null);
            Assert.That(message.Length, Is.Zero);
        }

        [Test]
        public void IncompleteFrame_IsNotDequeued()
        {
            var queue = new ReceivePduQueue();
            var frame = Frame(Payload(100));

            queue.Enqueue(frame.Take(frame.Length - 1).ToArray());

            Assert.That(queue.IsEmpty, Is.True);
            Assert.That(queue.DequeueOrNull(), Is.Null);
        }

        [Test]
        public void FrameLargerThanLimit_Throws()
        {
            var queue = new ReceivePduQueue(maxFrameLength: 16);

            Assert.Throws<InvalidFrameLengthException>(() => queue.Enqueue(Frame(Payload(17))));
        }

        [Test]
        public void FrameOfExactlyMaxLength_IsAccepted()
        {
            var queue = new ReceivePduQueue(maxFrameLength: 16);
            var payload = Payload(16);

            queue.Enqueue(Frame(payload));

            Assert.That(ReadAll(queue.DequeueOrNull()), Is.EqualTo(payload));
        }

        [Test]
        public void NegativeFrameLength_Throws()
        {
            var queue = new ReceivePduQueue();
            var header = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(header, -1);

            Assert.Throws<InvalidFrameLengthException>(() => queue.Enqueue(header));
        }

        [Test]
        public void NonPositiveMaxFrameLength_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReceivePduQueue(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReceivePduQueue(-5));
        }

        [Test]
        public void ManySmallFrames_AllSurviveInOrder()
        {
            var queue = new ReceivePduQueue();
            const int framesCount = 500;

            for (var i = 0; i < framesCount; i++)
                queue.Enqueue(Frame(BitConverter.GetBytes(i)));

            for (var i = 0; i < framesCount; i++)
            {
                var message = queue.DequeueOrNull();
                Assert.That(message, Is.Not.Null, $"Frame {i} is missing");
                Assert.That(BitConverter.ToInt32(ReadAll(message)), Is.EqualTo(i));
            }

            Assert.That(queue.DequeueOrNull(), Is.Null);
        }
    }
}
