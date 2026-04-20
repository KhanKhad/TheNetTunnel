using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheNetTunnel.Presentation.Deserializers
{
    public class HelloMessageResponseDeserializer : DeserializerBase<HelloMessageResponse>
    {
        public override HelloMessageResponse DeserializeT(System.IO.Stream stream, int size)
        {
            return HelloMessageResponse.DeserializeFromStream(stream, size);
        }
    }
}
