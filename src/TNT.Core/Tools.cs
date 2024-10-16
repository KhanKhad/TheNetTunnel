using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace TNT.Core
{
    public static class Tools
    {
        public readonly static byte[] ZeroBuffer4 = new byte[4];

        public static void SetToArray<T>(this T str, byte[] array, int offset, int size = -1)
        {
            if (size == -1)
                size = Marshal.SizeOf(str);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, array, offset, size);
            Marshal.FreeHGlobal(ptr);
        }

        public static T ToStruct<T>(this byte[] array, int offset, int size = -1)
        {
            if (size == -1)
                size = Marshal.SizeOf(typeof(T));
            IntPtr p = Marshal.AllocHGlobal(size);
            Marshal.Copy(array, offset, p, size);
            T ans = (T) Marshal.PtrToStructure(p, typeof(T));
            Marshal.FreeHGlobal(p);
            return ans;
        }

        public static void WriteUint(this MemoryStream stream, uint value)
        {
            var val = BitConverter.GetBytes(value);
            stream.Write(val, 0, val.Length);
        }

        public static void WriteInt(this MemoryStream stream, int value)
        {
            var val = BitConverter.GetBytes(value);
            stream.Write(val, 0, val.Length);
        }

        public static bool TryReadInt(this MemoryStream from, out int value)
        {
            value = 0;
            var size = sizeof(int);
            if (@from.Length - @from.Position < size)
                return false;
            var buff = new byte[size];

            @from.Read(buff, 0, size);

            value = BitConverter.ToInt32(buff);

            return true;
        }

        public static void WriteShort(short outputMessageId, MemoryStream to)
        {
            //Write first byte
            to.WriteByte((byte)(outputMessageId & 0xFF));
            //Write second byte
            to.WriteByte((byte)(outputMessageId >> 8));
        }
        
        public static short? TryReadShort(this MemoryStream from)
        {
            if (@from.Length - @from.Position < sizeof(short))
                return null;
            return @from.ReadShort();
        }

        public static bool TryReadShort(this MemoryStream from, out short value)
        {
            value = 0;
            if (@from.Length - @from.Position < sizeof(short))
                return false;
            value = @from.ReadShort();
            return true;
        }

        public static short ReadShort(this MemoryStream from)
        {
            if (@from.Length - @from.Position < 2)
                throw new EndOfStreamException();

            var b0 = @from.ReadByte();
            var b1 = @from.ReadByte();

            return (short)(b0 | b1 << 8);
        }

        public static void CopyToAnotherStream(this Stream stream, Stream targetStream, int lenght)
        {
            int lasts = lenght;

            byte[] arr = ArrayPool<byte>.Shared.Rent(4096);
            while (lasts > 0)
            {
                var lenghtB = lasts > 4096 ? 4096 : lasts;
                stream.Read(arr, 0, lenghtB);
                targetStream.Write(arr, 0, lenghtB);
                lasts -= lenghtB;
            }
        }

        public static void WriteToStream<T>(this T str, Stream stream, int size = -1)
        {
            if (size == -1)
                size = Marshal.SizeOf(str);
            var arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            stream.Write(arr, 0, size);
        }
    }
}
