using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TNT.Core.Presentation
{
    public class HelloMessageRequest
    {
        public string HelloMessageVersion { get; set; } = "A";

        public Version MyVersion { get; set; }
        public Version MinimalVersion { get; set; }

        public HelloMessageRequest() { }

        public void SerializeToStream(Stream stream)
        {
            var json = JsonSerializer.Serialize(this);
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
        }

        public static HelloMessageRequest DeserializeFromStream(Stream stream, int size)
        {
            var sharedArray = ArrayPool<byte>.Shared.Rent(size);
            Array.Clear(sharedArray, 0, size);

            stream.Read(sharedArray, 0, size);
            var json = Encoding.UTF8.GetString(sharedArray, 0, size);
            ArrayPool<byte>.Shared.Return(sharedArray);

            return JsonSerializer.Deserialize<HelloMessageRequest>(json);
        }
    }

    public class HelloMessageResponse
    {
        public string HelloMessageVersion { get; set; } = "A";
        public bool AvailableForWork { get; set; }
        public string UnavailabilityReason { get; set; } = string.Empty;

        public HelloMessageResponse() { }

        public void SerializeToStream(Stream stream)
        {
            var json = JsonSerializer.Serialize(this);
            var bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
        }
        public static HelloMessageResponse DeserializeFromStream(Stream stream, int size)
        {
            var sharedArray = ArrayPool<byte>.Shared.Rent(size);
            Array.Clear(sharedArray, 0, size);

            stream.Read(sharedArray, 0, size);
            var json = Encoding.UTF8.GetString(sharedArray, 0, size);
            ArrayPool<byte>.Shared.Return(sharedArray);

            return JsonSerializer.Deserialize<HelloMessageResponse>(json);
        }

        public static HelloMessageResponse Available()
        {
            return new HelloMessageResponse
            {
                AvailableForWork = true
            };
        }

        public static HelloMessageResponse UnavailableVersion()
        {
            return new HelloMessageResponse
            {
                AvailableForWork = false,
                UnavailabilityReason = "Version is not supported"
            };
        }

        public static HelloMessageResponse ConnectionsLimit()
        {
            return new HelloMessageResponse
            {
                AvailableForWork = false,
                UnavailabilityReason = "Connections limit reached"
            };
        }

        public static HelloMessageResponse UnknownReason()
        {
            return new HelloMessageResponse
            {
                AvailableForWork = false,
                UnavailabilityReason = "Unknown reason"
            };
        }
    }
}
