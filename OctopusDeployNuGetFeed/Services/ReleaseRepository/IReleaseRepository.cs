using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;
using OctopusDeployNuGetFeed.Model;

namespace OctopusDeployNuGetFeed.Services.ReleaseRepository
{
    public interface IReleaseRepository : IService
    {
        Task<IEnumerable<ODataPackage>> GetAllReleasesAsync(string projectName);
        Task<ODataPackage> FindLatestReleaseAsync(string projectName);
        Task<ODataPackage> GetReleaseAsync(string projectName, string version);
        Task<ODataPackageFile> GetPackageAsync(string projectName, string version);
    }
}