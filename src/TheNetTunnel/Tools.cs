using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;

namespace TheNetTunnel
{
    public static class Tools
    {
        private const int CopyChunkSize = 4096;

        public static void WriteBool(this Stream stream, bool value)
        {
            stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        public static void WriteShort(this Stream stream, short value)
        {
            Span<byte> buf = stackalloc byte[sizeof(short)];
            BinaryPrimitives.WriteInt16LittleEndian(buf, value);
            stream.Write(buf);
        }

        public static void WriteShort(short value, Stream to) => to.WriteShort(value);

        public static void WriteUshort(this Stream stream, ushort value)
        {
            Span<byte> buf = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
            stream.Write(buf);
        }

        public static void WriteInt(this Stream stream, int value)
        {
            Span<byte> buf = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buf, value);
            stream.Write(buf);
        }

        public static void WriteUint(this Stream stream, uint value)
        {
            Span<byte> buf = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
            stream.Write(buf);
        }

        public static void WriteLong(this Stream stream, long value)
        {
            Span<byte> buf = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(buf, value);
            stream.Write(buf);
        }

        public static void WriteUlong(this Stream stream, ulong value)
        {
            Span<byte> buf = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(buf, value);
            stream.Write(buf);
        }

        public static void WriteFloat(this Stream stream, float value)
        {
            Span<byte> buf = stackalloc byte[sizeof(float)];
            BinaryPrimitives.WriteSingleLittleEndian(buf, value);
            stream.Write(buf);
        }

        public static void WriteDouble(this Stream stream, double value)
        {
            Span<byte> buf = stackalloc byte[sizeof(double)];
            BinaryPrimitives.WriteDoubleLittleEndian(buf, value);
            stream.Write(buf);
        }

        public static bool TryReadInt(this Stream stream, out int value)
        {
            Span<byte> buf = stackalloc byte[sizeof(int)];
            if (!TryReadExact(stream, buf))
            {
                value = 0;
                return false;
            }
            value = BinaryPrimitives.ReadInt32LittleEndian(buf);
            return true;
        }

        public static bool TryReadShort(this Stream stream, out short value)
        {
            Span<byte> buf = stackalloc byte[sizeof(short)];
            if (!TryReadExact(stream, buf))
            {
                value = 0;
                return false;
            }
            value = BinaryPrimitives.ReadInt16LittleEndian(buf);
            return true;
        }

        public static short ReadShort(this Stream stream)
        {
            if (!TryReadShort(stream, out var value))
                throw new EndOfStreamException();
            return value;
        }

        public static void CopyToAnotherStream(this Stream stream, Stream targetStream, int length)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(CopyChunkSize);
            try
            {
                while (length > 0)
                {
                    var toCopy = length > CopyChunkSize ? CopyChunkSize : length;
                    var read = stream.Read(buffer, 0, toCopy);
                    if (read == 0)
                        throw new EndOfStreamException();
                    targetStream.Write(buffer, 0, read);
                    length -= read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static void WriteToStream<T>(this T value, Stream stream, int size = -1) where T : struct
        {
            switch (value)
            {
                case bool v: stream.WriteBool(v); return;
                case byte v: stream.WriteByte(v); return;
                case sbyte v: stream.WriteByte((byte)v); return;
                case short v: stream.WriteShort(v); return;
                case ushort v: stream.WriteUshort(v); return;
                case int v: stream.WriteInt(v); return;
                case uint v: stream.WriteUint(v); return;
                case long v: stream.WriteLong(v); return;
                case ulong v: stream.WriteUlong(v); return;
                case float v: stream.WriteFloat(v); return;
                case double v: stream.WriteDouble(v); return;
                case char v: stream.WriteUshort(v); return;
                case TimeSpan v: stream.WriteLong(v.Ticks); return;
                default: throw new NotSupportedException($"Binary serialization of {typeof(T)} is not supported");
            }
        }

        public static int SizeOfPrimitive(Type type)
        {
            if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte)) return 1;
            if (type == typeof(short) || type == typeof(ushort) || type == typeof(char)) return 2;
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float)) return 4;
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double)) return 8;
            if (type == typeof(TimeSpan)) return sizeof(long);
            throw new NotSupportedException($"{type} is not a supported primitive type");
        }

        public static T ToStruct<T>(this byte[] array, int offset, int size = -1) where T : struct
        {
            var t = typeof(T);
            var span = array.AsSpan(offset);

            if (t == typeof(bool)) return (T)(object)(array[offset] != 0);
            if (t == typeof(byte)) return (T)(object)array[offset];
            if (t == typeof(sbyte)) return (T)(object)(sbyte)array[offset];
            if (t == typeof(short)) return (T)(object)BinaryPrimitives.ReadInt16LittleEndian(span);
            if (t == typeof(ushort)) return (T)(object)BinaryPrimitives.ReadUInt16LittleEndian(span);
            if (t == typeof(int)) return (T)(object)BinaryPrimitives.ReadInt32LittleEndian(span);
            if (t == typeof(uint)) return (T)(object)BinaryPrimitives.ReadUInt32LittleEndian(span);
            if (t == typeof(long)) return (T)(object)BinaryPrimitives.ReadInt64LittleEndian(span);
            if (t == typeof(ulong)) return (T)(object)BinaryPrimitives.ReadUInt64LittleEndian(span);
            if (t == typeof(float)) return (T)(object)BinaryPrimitives.ReadSingleLittleEndian(span);
            if (t == typeof(double)) return (T)(object)BinaryPrimitives.ReadDoubleLittleEndian(span);
            if (t == typeof(char)) return (T)(object)(char)BinaryPrimitives.ReadUInt16LittleEndian(span);
            if (t == typeof(TimeSpan)) return (T)(object)TimeSpan.FromTicks(BinaryPrimitives.ReadInt64LittleEndian(span));

            throw new NotSupportedException($"Binary deserialization of {typeof(T)} is not supported");
        }

        private static bool TryReadExact(Stream stream, Span<byte> destination)
        {
            var total = 0;
            while (total < destination.Length)
            {
                var read = stream.Read(destination.Slice(total));
                if (read == 0)
                    return false;
                total += read;
            }
            return true;
        }
    }
}
