using System;

namespace TNT.Core.Presentation.Serializers
{
    public class ProtoSerializer<T> : SerializerBase<T>
    {
        public ProtoSerializer()
        {
            Size = null;
        }

        public override void SerializeT(T obj, System.IO.MemoryStream stream)
        {
            //write length prefix
            var postion = stream.Position;
            stream.Write(Tools.ZeroBuffer4, 0, 4);

            //protobuf-serializer writes length prefix too slowly
            ProtoBuf.Serializer.SerializeWithLengthPrefix<T>(stream, obj, ProtoBuf.PrefixStyle.None);

            //roll stream back and write the length
            var resultPostion = stream.Position;
            var length = resultPostion - postion - 4;
            stream.Position = postion;
            stream.Write(BitConverter.GetBytes(length), 0, 4);
            //return stream position
            stream.Position = resultPostion;
        }

        public override void Serialize(object obj, System.IO.MemoryStream stream)
        {
            SerializeT((T) obj, stream);
        }
    }
}

