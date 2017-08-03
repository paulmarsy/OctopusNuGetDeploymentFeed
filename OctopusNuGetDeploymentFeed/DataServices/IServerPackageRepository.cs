using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IServerPackageRepository
    {
        bool IsAuthenticated { get; }
        string BaseUri { get; }
        string ApiKey { get; }
        IEnumerable<INuGetPackage> FindOctopusReleasePackages(string name, CancellationToken token);
        IDownloadableNuGetPackage GetOctopusReleasePackage(string name, string version, CancellationToken token);
        IEnumerable<INuGetPackage> FindOctopusProjectPackages(string searchTerm, CancellationToken token);

    }
}