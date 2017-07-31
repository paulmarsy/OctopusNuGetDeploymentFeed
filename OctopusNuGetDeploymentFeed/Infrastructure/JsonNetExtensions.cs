using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public static class JsonNetExtensions
    {
        private static readonly JsonSerializer JsonSerializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.Indented
        };

        public static void SerializeInto(this object @object, Stream stream)
        {
            using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, 1024, true))
            using (var jsonTextWriter = new JsonTextWriter(streamWriter))
            {
                JsonSerializer.Serialize(jsonTextWriter, @object);
            }
        }
    }
}