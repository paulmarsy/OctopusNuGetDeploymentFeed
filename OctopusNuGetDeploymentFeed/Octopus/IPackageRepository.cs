using System.Collections.Generic;
using System.Threading;
using OctopusDeployNuGetFeed.Octopus.Packages;

namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IPackageRepository
    {
        IEnumerable<INuGetPackage> FindProjectReleases(string projectName, CancellationToken token);
        IDownloadableNuGetPackage GetRelease(string projectName, string version, CancellationToken token);
        IEnumerable<INuGetPackage> FindProjects(string searchTerm, CancellationToken token);
    }
}