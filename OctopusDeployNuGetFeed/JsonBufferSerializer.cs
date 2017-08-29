using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace OctopusDeployNuGetFeed
{
    public static class JsonBufferSerializer
    {
        private static readonly JsonSerializer JsonSerializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.All,
            Formatting = Formatting.None,
            PreserveReferencesHandling = PreserveReferencesHandling.All
        };

        public static byte[] Serialize(object @object)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8))
                using (var jsonTextWriter = new JsonTextWriter(streamWriter))
                {
                    JsonSerializer.Serialize(jsonTextWriter, @object);
                }
                return memoryStream.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] buffer)
        {
            using (var memoryStream = new MemoryStream(buffer))
            using (var streamReader = new StreamReader(memoryStream, Encoding.UTF8))
            using (var jsonTextReader = new JsonTextReader(streamReader))
            {
                return JsonSerializer.Deserialize<T>(jsonTextReader);
            }
        }
    }
}