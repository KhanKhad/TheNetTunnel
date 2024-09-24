using System;

namespace TNT.Core.Presentation.Serializers
{
    public class UTCFileTimeAndOffsetSerializer : SerializerBase<DateTimeOffset>
    {
        private static readonly ValueTypeSerializer<int> IntSerializer = new ValueTypeSerializer<int>();
        public UTCFileTimeAndOffsetSerializer()
        {
            Size = sizeof(long) + IntSerializer.Size;
        }

        public override void SerializeT(DateTimeOffset timeOffset, System.IO.MemoryStream stream)
        {
            if (timeOffset.Year < 1602)
            {
                WriteDefaultUnixTimeTo(stream);
            }
            else
            {
                var lng = timeOffset.DateTime.ToFileTimeUtc();
                lng.WriteToStream(stream, sizeof(long));

                var offset = (int) timeOffset.Offset.TotalSeconds;
                IntSerializer.SerializeT(offset, stream);
            }
        }

        private static void WriteDefaultUnixTimeTo(System.IO.MemoryStream stream)
        {
            // Допустимое значение для даты это 1601 год с.м описание ToFileTimeUtc()
            // Значит берем дату от балды, в системе не должно быть таких дат
            var time = new DateTime(1666,6,6,6,6,6,6);
            
            var lng = time.ToFileTimeUtc();
            lng.WriteToStream(stream, sizeof(long));

            const int offset = 666;
            IntSerializer.SerializeT(offset, stream);
        }
    }
}