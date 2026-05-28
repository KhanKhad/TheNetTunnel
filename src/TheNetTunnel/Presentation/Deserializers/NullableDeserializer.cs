using System.IO;

namespace TheNetTunnel.Presentation.Deserializers
{
    public class NullableDeserializer<T> : DeserializerBase<T?> where T : struct
    {
        public NullableDeserializer()
        {
            Size = Tools.SizeOfPrimitive(typeof(T)) + 1;
        }

        public override T? DeserializeT(Stream stream, int size)
        {
            var arr = new byte[Size.Value];
            stream.Read(arr, 0, Size.Value);
            if (arr[0] == 0)
                return null;
            return Tools.ToStruct<T>(arr, 1, Size.Value - 1);
        }
    }
}
