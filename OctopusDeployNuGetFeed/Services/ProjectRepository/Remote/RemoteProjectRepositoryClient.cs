using System.Collections.Generic;
using System.Threading.Tasks;
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.Remoting;
using OctopusDeployNuGetFeed.Services.ProjectRepository.Fabric;

namespace OctopusDeployNuGetFeed.Services.ProjectRepository.Remote
{
    public class RemoteProjectRepositoryClient : BaseRemotingClient<IProjectRepository, OctopusCredential>, IProjectRepository
    {
        public RemoteProjectRepositoryClient(OctopusCredential credential)
        {
            ContextObject = credential;
        }

        protected override OctopusCredential ContextObject { get; }
        public override string ServiceName => nameof(OctopusProjectRepositoryService);

        public Task<IEnumerable<ODataPackage>> GetAllProjectsAsync()
        {
            return GetProxy(ServiceName).GetAllProjectsAsync();
        }
    }
}