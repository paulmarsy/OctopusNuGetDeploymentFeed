using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed.Model
{
    public interface IDownloadableNuGetPackage : INuGetPackage
    {
        Task<byte[]> GetPackageBlob();
    }
}