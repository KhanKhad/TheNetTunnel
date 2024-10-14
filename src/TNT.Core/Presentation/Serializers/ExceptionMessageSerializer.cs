using System.IO;
using TNT.Core.Exceptions.Remote;
using TNT.Core.Presentation.Deserializers;

namespace TNT.Core.Presentation.Serializers
{
    public class ErrorMessageSerializer: SerializerBase<ErrorMessage>
    {
        private  readonly SequenceSerializer _serializer;

        public ErrorMessageSerializer()
        {
            this.Size = null;
            _serializer = new SequenceSerializer(
                   new ISerializer[]
                   {
                        new ValueTypeSerializer<short>(),
                        new ValueTypeSerializer<int>(),
                        new EnumSerializer<ErrorType>(),
                        new UnicodeSerializer()
                   });
        }
        public override void SerializeT(ErrorMessage obj, MemoryStream stream)
        {
             _serializer.SerializeT(
                    new object[]
                    {
                        obj.MessageId,
                        obj.AskId,
                        obj.ErrorType,
                        obj.AdditionalExceptionInformation
                    }, stream);

        }
    }
}
