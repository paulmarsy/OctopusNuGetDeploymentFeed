using System.IO;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IDownloadableNuGetPackage : INuGetPackage
    {
        Stream GetStream();
    }
}