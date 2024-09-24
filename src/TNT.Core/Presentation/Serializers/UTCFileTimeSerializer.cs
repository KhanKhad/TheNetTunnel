using System;

namespace TNT.Core.Presentation.Serializers
{
    public class UTCFileTimeSerializer : SerializerBase<DateTime>
    {
        public UTCFileTimeSerializer()
        {
            Size = sizeof(long);
        }

        public override void SerializeT(DateTime time, System.IO.MemoryStream stream)
        {
            if (time.Year < 1602)
            {
                WriteDefaultUnixTimeTo(stream);
            }
            else
            {
                var lng = time.ToFileTimeUtc();
                lng.WriteToStream(stream, Size.Value);
            }
        }

        private static void WriteDefaultUnixTimeTo(System.IO.MemoryStream stream)
        {
            // Допустимое значение для даты это > 1601 год с.м описание ToFileTimeUtc()
            // Значит берем дату от балды, в системе не должно быть таких дат
            var time = new DateTime(1666,6,6,6,6,6,6);
            var lng = time.ToFileTimeUtc();
            lng.WriteToStream(stream, sizeof(long));
        }
    }
}