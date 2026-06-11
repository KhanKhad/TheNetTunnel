using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using TheNetTunnel.Presentation;

namespace TheNetTunnel.Tests.Presentation
{
    [TestFixture]
    public class PooledMemoryStreamTests
    {
        [Test]
        public void WriteThenRead_RoundtripsData()
        {
            using var stream = new PooledMemoryStream();
            var data = new byte[] { 1, 2, 3, 4, 5 };

            stream.Write(data, 0, data.Length);
            stream.Position = 0;

            var read = new byte[data.Length];
            Assert.That(stream.Read(read, 0, read.Length), Is.EqualTo(data.Length));
            Assert.That(read, Is.EqualTo(data));
        }

        [Test]
        public void ReadPastEnd_ReturnsZero()
        {
            using var stream = new PooledMemoryStream();
            stream.WriteByte(7);

            var buffer = new byte[10];
            Assert.That(stream.Read(buffer, 0, buffer.Length), Is.Zero);
        }

        [Test]
        public void Read_ReturnsOnlyAvailableBytes()
        {
            using var stream = new PooledMemoryStream();
            stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
            stream.Position = 1;

            var buffer = new byte[10];
            Assert.That(stream.Read(buffer, 0, buffer.Length), Is.EqualTo(2));
            Assert.That(buffer[0], Is.EqualTo(2));
            Assert.That(buffer[1], Is.EqualTo(3));
        }

        [Test]
        public void GrowthBeyondInitialCapacity_PreservesContent()
        {
            using var stream = new PooledMemoryStream(16);
            var data = new byte[10_000];
            new Random(42).NextBytes(data);

            stream.Write(data, 0, data.Length);

            Assert.That(stream.Length, Is.EqualTo(data.Length));
            Assert.That(stream.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public void Seek_SupportsAllOrigins()
        {
            using var stream = new PooledMemoryStream();
            stream.Write(new byte[10], 0, 10);

            Assert.That(stream.Seek(3, SeekOrigin.Begin), Is.EqualTo(3));
            Assert.That(stream.Seek(2, SeekOrigin.Current), Is.EqualTo(5));
            Assert.That(stream.Seek(-1, SeekOrigin.End), Is.EqualTo(9));
        }

        [Test]
        public void SeekBeforeZero_Throws()
        {
            using var stream = new PooledMemoryStream();
            Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        }

        [Test]
        public void SetLength_Grow_ZeroFillsNewBytes()
        {
            using var stream = new PooledMemoryStream();
            stream.Write(new byte[] { 0xFF, 0xFF }, 0, 2);

            stream.SetLength(5);

            var content = stream.ToArray();
            Assert.That(content.Length, Is.EqualTo(5));
            Assert.That(content[2], Is.Zero);
            Assert.That(content[3], Is.Zero);
            Assert.That(content[4], Is.Zero);
        }

        [Test]
        public void SetLength_Shrink_ClampsPosition()
        {
            using var stream = new PooledMemoryStream();
            stream.Write(new byte[10], 0, 10);

            stream.SetLength(4);

            Assert.That(stream.Length, Is.EqualTo(4));
            Assert.That(stream.Position, Is.EqualTo(4));
        }

        [Test]
        public void WriteAfterSparseSeek_ZeroFillsGap()
        {
            using var stream = new PooledMemoryStream();
            stream.WriteByte(0xFF);

            stream.Position = 5;
            stream.WriteByte(0xAA);

            var content = stream.ToArray();
            Assert.That(content.Length, Is.EqualTo(6));
            Assert.That(content[0], Is.EqualTo(0xFF));
            for (var i = 1; i < 5; i++)
                Assert.That(content[i], Is.Zero, $"Gap byte {i} must be zero-filled");
            Assert.That(content[5], Is.EqualTo(0xAA));
        }

        [Test]
        public void ToArray_And_GetWrittenMemory_ReturnSameContent()
        {
            using var stream = new PooledMemoryStream();
            var data = new byte[] { 9, 8, 7 };
            stream.Write(data, 0, data.Length);

            Assert.That(stream.ToArray(), Is.EqualTo(data));
            Assert.That(stream.GetWrittenMemory().ToArray(), Is.EqualTo(data));
            Assert.That(stream.GetWrittenSegment().ToArray(), Is.EqualTo(data));
        }

        [Test]
        public void Dispose_MakesStreamUnusable()
        {
            var stream = new PooledMemoryStream();
            stream.WriteByte(1);

            stream.Dispose();

            Assert.That(stream.CanRead, Is.False);
            Assert.That(stream.CanWrite, Is.False);
            Assert.That(stream.CanSeek, Is.False);
            Assert.Throws<ObjectDisposedException>(() => stream.WriteByte(1));
            Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
            Assert.Throws<ObjectDisposedException>(() => stream.GetWrittenMemory());
            Assert.Throws<ObjectDisposedException>(() => _ = stream.Length);
        }

        [Test]
        public void DoubleDispose_DoesNotThrow()
        {
            var stream = new PooledMemoryStream();
            Assert.DoesNotThrow(() =>
            {
                stream.Dispose();
                stream.Dispose();
            });
        }

        [Test]
        public void Dispose_CancelsUncompletedTks()
        {
            var stream = new PooledMemoryStream
            {
                Tks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };

            stream.Dispose();

            Assert.That(stream.Tks.Task.IsCanceled, Is.True,
                "A message disposed without being sent must not look successfully sent");
        }

        [Test]
        public void Dispose_DoesNotOverrideSuccessfulTks()
        {
            var stream = new PooledMemoryStream
            {
                Tks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            stream.Tks.TrySetResult();

            stream.Dispose();

            Assert.That(stream.Tks.Task.IsCompletedSuccessfully, Is.True,
                "A success result must survive Dispose");
        }

        [Test]
        public void Dispose_DoesNotOverrideFaultedTks()
        {
            var stream = new PooledMemoryStream
            {
                Tks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            stream.Tks.TrySetException(new InvalidOperationException("send failed"));

            stream.Dispose();

            Assert.That(stream.Tks.Task.IsFaulted, Is.True,
                "A failure result must survive Dispose");
        }

        [Test]
        public void NegativeInitialCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PooledMemoryStream(-1));
        }
    }
}
