using System.IO;

namespace TheNetTunnel.Presentation.Serializers
{
    public class ByteArraySerializer :  SerializerBase<byte[]>
    {
        public ByteArraySerializer()
        {
            Size = null;
        }

        public override void SerializeT(byte[] obj, Stream stream)
        {
            if (obj == null)
                return;
            stream.Write(obj,0, obj.Length);
        }
    }
}
