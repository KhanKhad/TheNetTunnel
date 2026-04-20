using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheNetTunnel.Presentation.Serializers
{
    public class HelloMessageResponseSerializer : SerializerBase<HelloMessageResponse>
    {
        public HelloMessageResponseSerializer()
        {
            Size = null;
        }
        public override void SerializeT(HelloMessageResponse obj, MemoryStream stream)
        {
            obj.SerializeToStream(stream);
        }
    }
}
