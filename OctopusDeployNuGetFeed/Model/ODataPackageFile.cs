using System.Runtime.Serialization;

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
        public long PackageBlobSize { get; set; }

        [DataMember]
        public byte[] PackageBlob { get; set; }

        public static ODataPackageFile FromNuGetPackage(IDownloadableNuGetPackage package)
        {
            if (package == null)
                return null;
            return new ODataPackageFile
            {
                Id = package.Id,
                Version = package.Version,
                PackageBlobSize = package.BlobSize,
                PackageBlob = package.GetBlob()
            };
        }
    }
}