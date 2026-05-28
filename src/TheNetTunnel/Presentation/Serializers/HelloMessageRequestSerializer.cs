using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheNetTunnel.Presentation.Serializers
{
    public class HelloMessageRequestSerializer : SerializerBase<HelloMessageRequest>
    {
        public HelloMessageRequestSerializer() 
        {
            Size = null;
        }
        public override void SerializeT(HelloMessageRequest obj, Stream stream)
        {
            obj.SerializeToStream(stream);
        }
    }
}
