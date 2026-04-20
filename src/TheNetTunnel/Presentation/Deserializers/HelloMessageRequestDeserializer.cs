using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheNetTunnel.Presentation.Deserializers
{
    public class HelloMessageRequestDeserializer : DeserializerBase<HelloMessageRequest>
    {
        public override HelloMessageRequest DeserializeT(System.IO.Stream stream, int size)
        {
            return HelloMessageRequest.DeserializeFromStream(stream, size);
        }
    }
}
