using ProtoBuf;

namespace TNT.Core.Presentation.Deserializers
{
	public class ProtoDeserializer<T>: DeserializerBase<T>
        where T: new()
    {
	    public ProtoDeserializer()
		{
			Size = null;
        }

		public override T DeserializeT (System.IO.Stream stream, int size)
		{
            var ans = ProtoBuf.Serializer.DeserializeWithLengthPrefix<T>(stream, PrefixStyle.Fixed32);
            return ans;
		}
	}
}

