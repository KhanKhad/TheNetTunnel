using System;
using System.IO;
using NUnit.Framework;

namespace TheNetTunnel.Tests.Presentation
{
    [TestFixture]
    public class ToolsTests
    {
        private static T Roundtrip<T>(T value) where T : struct
        {
            using var stream = new MemoryStream();
            value.WriteToStream(stream);
            return stream.ToArray().ToStruct<T>(0);
        }

        [Test]
        public void PrimitiveRoundtrips_PreserveValues()
        {
            Assert.That(Roundtrip(true), Is.True);
            Assert.That(Roundtrip(false), Is.False);
            Assert.That(Roundtrip((byte)200), Is.EqualTo((byte)200));
            Assert.That(Roundtrip((sbyte)-100), Is.EqualTo((sbyte)-100));
            Assert.That(Roundtrip(short.MinValue), Is.EqualTo(short.MinValue));
            Assert.That(Roundtrip(ushort.MaxValue), Is.EqualTo(ushort.MaxValue));
            Assert.That(Roundtrip(int.MinValue), Is.EqualTo(int.MinValue));
            Assert.That(Roundtrip(uint.MaxValue), Is.EqualTo(uint.MaxValue));
            Assert.That(Roundtrip(long.MinValue), Is.EqualTo(long.MinValue));
            Assert.That(Roundtrip(ulong.MaxValue), Is.EqualTo(ulong.MaxValue));
            Assert.That(Roundtrip(float.Epsilon), Is.EqualTo(float.Epsilon));
            Assert.That(Roundtrip(double.MaxValue), Is.EqualTo(double.MaxValue));
            Assert.That(Roundtrip('я'), Is.EqualTo('я'));
            Assert.That(Roundtrip(TimeSpan.FromTicks(123456789)), Is.EqualTo(TimeSpan.FromTicks(123456789)));
        }

        [Test]
        public void WriteToStream_UnsupportedType_Throws()
        {
            using var stream = new MemoryStream();
            Assert.Throws<NotSupportedException>(() => 1.5m.WriteToStream(stream));
        }

        [Test]
        public void ToStruct_UnsupportedType_Throws()
        {
            Assert.Throws<NotSupportedException>(() => new byte[16].ToStruct<decimal>(0));
        }

        [TestCase(typeof(bool), 1)]
        [TestCase(typeof(byte), 1)]
        [TestCase(typeof(sbyte), 1)]
        [TestCase(typeof(short), 2)]
        [TestCase(typeof(ushort), 2)]
        [TestCase(typeof(char), 2)]
        [TestCase(typeof(int), 4)]
        [TestCase(typeof(uint), 4)]
        [TestCase(typeof(float), 4)]
        [TestCase(typeof(long), 8)]
        [TestCase(typeof(ulong), 8)]
        [TestCase(typeof(double), 8)]
        [TestCase(typeof(TimeSpan), 8)]
        public void SizeOfPrimitive_ReturnsWireSize(Type type, int expectedSize)
        {
            Assert.That(Tools.SizeOfPrimitive(type), Is.EqualTo(expectedSize));
        }

        [Test]
        public void SizeOfPrimitive_UnsupportedType_Throws()
        {
            Assert.Throws<NotSupportedException>(() => Tools.SizeOfPrimitive(typeof(string)));
        }

        [Test]
        public void TryReadInt_OnTruncatedStream_ReturnsFalse()
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            Assert.That(stream.TryReadInt(out _), Is.False);
        }

        [Test]
        public void TryReadShort_OnTruncatedStream_ReturnsFalse()
        {
            using var stream = new MemoryStream(new byte[] { 1 });
            Assert.That(stream.TryReadShort(out _), Is.False);
        }

        [Test]
        public void TryReadInt_ReadsLittleEndianValue()
        {
            using var stream = new MemoryStream();
            stream.WriteInt(int.MinValue);
            stream.Position = 0;

            Assert.That(stream.TryReadInt(out var value), Is.True);
            Assert.That(value, Is.EqualTo(int.MinValue));
        }

        [Test]
        public void TryReadShort_ReadsLittleEndianValue()
        {
            using var stream = new MemoryStream();
            stream.WriteShort(short.MinValue);
            stream.Position = 0;

            Assert.That(stream.TryReadShort(out var value), Is.True);
            Assert.That(value, Is.EqualTo(short.MinValue));
        }

        [Test]
        public void ReadShort_OnEmptyStream_Throws()
        {
            using var stream = new MemoryStream();
            Assert.Throws<EndOfStreamException>(() => stream.ReadShort());
        }

        [Test]
        public void CopyToAnotherStream_CopiesRequestedLength()
        {
            var data = new byte[10_000];
            new Random(7).NextBytes(data);

            using var source = new MemoryStream(data);
            using var target = new MemoryStream();

            source.CopyToAnotherStream(target, 6000);

            Assert.That(target.Length, Is.EqualTo(6000));
            Assert.That(target.ToArray(), Is.EqualTo(data.AsSpan(0, 6000).ToArray()));
            Assert.That(source.Position, Is.EqualTo(6000), "Source must stop right after the copied range");
        }

        [Test]
        public void CopyToAnotherStream_SourceTooShort_Throws()
        {
            using var source = new MemoryStream(new byte[10]);
            using var target = new MemoryStream();

            Assert.Throws<EndOfStreamException>(() => source.CopyToAnotherStream(target, 11));
        }
    }
}
