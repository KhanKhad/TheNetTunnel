using System.IO;
using TNT.Core.Exceptions.Remote;

namespace TNT.Core.Presentation.Deserializers
{
    public class ErrorMessageDeserializer: DeserializerBase<ErrorMessage>
    {
        private readonly SequenceDeserializer _deserializer;

        public ErrorMessageDeserializer()
        {
            Size = null;
            _deserializer = new SequenceDeserializer(new IDeserializer[]
            {
                  new ValueTypeDeserializer<short>(),
                  new ValueTypeDeserializer<int>(),
                  new EnumDeserializer<ErrorType>(),
                  new UnicodeDeserializer()
            });
        }
        public override ErrorMessage DeserializeT(Stream stream, int size)
        {
            var deserialized = _deserializer.DeserializeT(stream, size);
            return new ErrorMessage
            (
                messageId: (short)    deserialized[0],
                askId:     (int)    deserialized[1],
                type:      (ErrorType) deserialized[2],
                additionalExceptionInformation: (string) deserialized[3]
            );
        }
    }
}
