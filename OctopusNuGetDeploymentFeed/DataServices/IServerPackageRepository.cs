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
        Task<IEnumerable<IServerPackage>> GetProjectReleasesAsync(string id, CancellationToken token);
        Task<IServerPackage> GetProjectReleaseAsync(string id, string version, CancellationToken token);

        Task<IEnumerable<IServerPackage>> FindProjectsAsync(string searchTerm, CancellationToken token);
    }
}