using System.IO;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IDownloadableNuGetPackage : INuGetPackage
    {
        long PackageSize { get; }
        Stream GetStream();
    }
}