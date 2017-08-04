using System.Collections.Generic;
using System.Threading;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IOctopusServer
    {
        bool IsAuthenticated { get; }
        string BaseUri { get; }
        string ApiKey { get; }
    }
    public interface IPackageRepository
    {
    
        IEnumerable<INuGetPackage> FindOctopusReleasePackages(string name, CancellationToken token);
        IDownloadableNuGetPackage GetOctopusReleasePackage(string name, string version, CancellationToken token);
        IEnumerable<INuGetPackage> FindOctopusProjectPackages(string searchTerm, CancellationToken token);
        bool IsAuthenticated { get; }
    }
}