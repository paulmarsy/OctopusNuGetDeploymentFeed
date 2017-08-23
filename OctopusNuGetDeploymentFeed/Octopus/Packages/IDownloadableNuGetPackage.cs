using System.IO;

namespace OctopusDeployNuGetFeed.Octopus.Packages
{
    public interface IDownloadableNuGetPackage : INuGetPackage
    {
        long PackageSize { get; }
        Stream GetStream();
    }
}