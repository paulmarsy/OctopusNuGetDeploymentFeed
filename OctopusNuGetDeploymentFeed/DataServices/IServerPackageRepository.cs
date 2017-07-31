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
        Task<IEnumerable<IServerPackage>> GetPackagesAsync(string id, bool allowPrereleaseVersions, CancellationToken token);
        Task<IServerPackage> GetPackageVersionAsync(string id, string version, CancellationToken token);

        Task<IEnumerable<IServerPackage>> FindPackagesAsync(string searchTerm, bool allowPrereleaseVersions, CancellationToken token);
    }
}