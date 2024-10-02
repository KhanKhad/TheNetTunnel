using System;
using System.IO;
using TNT.Core.Presentation.Deserializers;

namespace TNT.Core.Presentation
{
    public class MessageTypeInfo
    {
        public Type[] ArgumentTypes;
        public Type ReturnType;
        public short MessageId;
    }


    internal class InputMessageDeserializeInfo
    {
        public static InputMessageDeserializeInfo CreateForAnswer(IDeserializer deserializer)
        {
            return new InputMessageDeserializeInfo(1, false, deserializer);
        }

        public static InputMessageDeserializeInfo CreateForAsk(int argumentsCount, bool hasReturnType,
            IDeserializer deserializer)
        {
            return new InputMessageDeserializeInfo(argumentsCount, hasReturnType, deserializer);
        }

        public static InputMessageDeserializeInfo CreateForExceptionHandling()
        {
            return new InputMessageDeserializeInfo(1, false, new ErrorMessageDeserializer());
        }

        private InputMessageDeserializeInfo(int argumentsCount, bool hasReturnType, IDeserializer deserializer)
        {
            ArgumentsCount = argumentsCount;
            HasReturnType = hasReturnType;
            Deserializer = deserializer;
        }


        public int ArgumentsCount { get; }
        public IDeserializer Deserializer { get; }
        public bool HasReturnType { get; }

        public object[] Deserialize(MemoryStream data)
        {
            object[] arg = null;
            if (ArgumentsCount == 0)
                arg = Array.Empty<object>();
            else if (ArgumentsCount == 1)
                arg = new[] {Deserializer.Deserialize(data, (int) (data.Length - data.Position))};
            else
                arg = (object[]) Deserializer.Deserialize(data, (int) (data.Length - data.Position));
            return arg;
        }
    }


}