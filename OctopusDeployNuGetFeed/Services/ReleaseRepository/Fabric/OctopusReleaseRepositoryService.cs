using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.Remoting;

namespace OctopusDeployNuGetFeed.Services.ReleaseRepository.Fabric
{
    public class OctopusReleaseRepositoryService : BaseRemotingService<OctopusCredential>, IReleaseRepository
    {
        private readonly IReleaseRepositoryFactory _factory;

        public OctopusReleaseRepositoryService(StatefulServiceContext context, string replicatorSettingsSectionName, OctopusReleaseRepositoryFactory factory) : base(context, replicatorSettingsSectionName)
        {
            _factory = factory;
        }

        public override string ServiceName => nameof(OctopusReleaseRepositoryService);

        public Task<IEnumerable<ODataPackage>> GetAllReleasesAsync(string projectName)
        {
            return _factory.GetReleaseRepository(ContextObject).GetAllReleasesAsync(projectName);
        }

        public Task<ODataPackage> FindLatestReleaseAsync(string projectName)
        {
            return _factory.GetReleaseRepository(ContextObject).FindLatestReleaseAsync(projectName);
        }

        public Task<ODataPackage> GetReleaseAsync(string projectName, string version)
        {
            return _factory.GetReleaseRepository(ContextObject).GetReleaseAsync(projectName, version);
        }

        public Task<ODataPackageFile> GetPackageAsync(string projectName, string version)
        {
            return _factory.GetReleaseRepository(ContextObject).GetPackageAsync(projectName, version);
        }
    }
}