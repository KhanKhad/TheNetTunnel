using System.IO;

namespace TheNetTunnel.Presentation.Serializers
{
    public abstract class SerializerBase<T> : ISerializer<T>
    {
        public abstract void SerializeT(T obj, Stream stream);

        public virtual void Serialize(object obj, Stream stream)
        {
            SerializeT((T) obj, stream);
        }

        public int? Size { get; protected set; }
    }
}
