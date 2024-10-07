using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TNT.Core.Presentation.Deserializers
{
    public class TaskDeserializer : DeserializerBase<Task>
    {
        public override Task DeserializeT(Stream stream, int size)
        {
            return null;
        }
    }
}
