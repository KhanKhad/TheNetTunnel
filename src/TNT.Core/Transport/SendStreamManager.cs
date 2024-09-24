using System.IO;

namespace TNT.Core.Transport
{
    public class SendStreamManager
    {
        private static readonly byte[] _reservedEmptyBuffer = new byte[sizeof(uint)];

        public MemoryStream CreateStreamForSend()
        {
            var stream = new MemoryStream(1024);

            if (ReservedHeadLength > 0)
                stream.Write(_reservedEmptyBuffer, 0, ReservedHeadLength);
            return stream;
        }
        public int ReservedHeadLength => sizeof(uint);

        public void PrepareForSending(MemoryStream message)
        {
            message.Position = 0;
            uint len = (uint)(message.Length - ReservedHeadLength);
            message.WriteInt(len);

            message.Position = 0;
        }
    }
}