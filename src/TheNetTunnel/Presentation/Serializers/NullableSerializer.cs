using System.IO;

namespace TheNetTunnel.Presentation.Serializers
{
    public class NullableSerializer<T>: SerializerBase<T?> where T: struct
    {
        public NullableSerializer()
        {
            Size = Tools.SizeOfPrimitive(typeof(T)) + 1;
        }

        public override void SerializeT(T? obj, Stream stream)
        {
            if (obj == null)
            {
                for (var i = 0; i < Size.Value; i++)
                    stream.WriteByte(0);
            }
            else
            {
                stream.WriteByte(1);
                obj.Value.WriteToStream(stream, Size.Value - 1);
            }
        }
    }
}
