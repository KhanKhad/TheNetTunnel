using System.IO;

namespace TheNetTunnel.Presentation.Serializers
{
    public class ValueTypeSerializer<T> : SerializerBase<T> where T: struct
    {
        public ValueTypeSerializer()
        {
            Size = Tools.SizeOfPrimitive(typeof(T));
        }

        public override void SerializeT(T obj, Stream stream)
        {
            obj.WriteToStream(stream, Size.Value);
        }
    }
}
