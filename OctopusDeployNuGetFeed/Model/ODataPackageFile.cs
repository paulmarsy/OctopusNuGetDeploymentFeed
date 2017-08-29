using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed.Model
{
    [DataContract]
    public class ODataPackageFile
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string Version { get; set; }

        [DataMember]
        public int PackageBlobSize { get; set; }

        [DataMember]
        public byte[] PackageBlob { get; set; }
        public static async Task<ODataPackageFile> FromNuGetPackage(IDownloadableNuGetPackage package)
        {
            if (package == null)
                return null;

            var blob = await package.GetPackageBlob();
            return new ODataPackageFile
            {
                Id = package.Id,
                Version = package.Version,
                PackageBlobSize =blob.Length,
                PackageBlob = blob
            };
        }
    }
}