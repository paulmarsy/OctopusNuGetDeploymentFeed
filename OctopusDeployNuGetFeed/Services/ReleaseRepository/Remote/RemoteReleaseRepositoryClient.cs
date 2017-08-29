using System.Collections.Generic;
using System.Threading.Tasks;
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.Remoting;
using OctopusDeployNuGetFeed.Services.ReleaseRepository.Fabric;

namespace OctopusDeployNuGetFeed.Services.ReleaseRepository.Remote
{
    public class RemoteReleaseRepositoryClient : BaseRemotingClient<IReleaseRepository, OctopusCredential>, IReleaseRepository
    {
        public RemoteReleaseRepositoryClient(OctopusCredential credential)
        {
            ContextObject = credential;
        }

        protected override OctopusCredential ContextObject { get; }
        public override string ServiceName => nameof(OctopusReleaseRepositoryService);

        public Task<IEnumerable<ODataPackage>> GetAllReleasesAsync(string projectName)
        {
            return GetProxy(projectName).GetAllReleasesAsync(projectName);
        }

        public Task<ODataPackage> FindLatestReleaseAsync(string projectName)
        {
            return GetProxy(projectName).FindLatestReleaseAsync(projectName);
        }

        public Task<ODataPackageFile> GetPackageAsync(string projectName, string version)
        {
            return GetProxy(projectName).GetPackageAsync(projectName, version);
        }

        public Task<ODataPackage> GetReleaseAsync(string projectName, string version)
        {
            return GetProxy(projectName).GetReleaseAsync(projectName, version);
        }
    }
}