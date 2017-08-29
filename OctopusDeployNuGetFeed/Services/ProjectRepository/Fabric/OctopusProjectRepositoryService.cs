using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.Remoting;

namespace OctopusDeployNuGetFeed.Services.ProjectRepository.Fabric
{
    public class OctopusProjectRepositoryService : BaseRemotingService<OctopusCredential>, IProjectRepository
    {
        private readonly IProjectRepositoryFactory _factory;

        public OctopusProjectRepositoryService(StatefulServiceContext serviceContext, string replicatorSettingsSectionName, IProjectRepositoryFactory factory) : base(serviceContext, replicatorSettingsSectionName)
        {
            _factory = factory;
        }

        public override string ServiceName => nameof(OctopusProjectRepositoryService);

        public Task<IEnumerable<ODataPackage>> GetAllProjectsAsync()
        {
            return _factory.GetProjectRepository(ContextObject).GetAllProjectsAsync();
        }
    }
}