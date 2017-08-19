using System.Collections.Generic;
using System.Threading;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IPackageRepository
    {
        bool IsAuthenticated { get; }
        IEnumerable<INuGetPackage> FindProjectReleases(string projectName, CancellationToken token);
        IDownloadableNuGetPackage GetRelease(string projectName, string version, CancellationToken token);
        IEnumerable<INuGetPackage> FindProjects(string searchTerm, CancellationToken token);
    }
}