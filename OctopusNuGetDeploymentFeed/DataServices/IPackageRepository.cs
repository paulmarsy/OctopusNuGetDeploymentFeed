using System.Collections.Generic;
using System.Threading;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IPackageRepository
    {
        bool IsAuthenticated { get; }
        IEnumerable<INuGetPackage> FindOctopusReleasePackages(string name, CancellationToken token);
        IDownloadableNuGetPackage GetOctopusReleasePackage(string name, string version, CancellationToken token);
        IEnumerable<INuGetPackage> FindOctopusProjectPackages(string searchTerm, CancellationToken token);
    }
}