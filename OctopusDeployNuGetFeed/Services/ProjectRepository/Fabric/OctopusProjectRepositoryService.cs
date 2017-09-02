using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.Remoting;
using OctopusDeployNuGetFeed.Services.AdminActor.Fabric;

namespace OctopusDeployNuGetFeed.Services.ProjectRepository.Fabric
{
    public class OctopusProjectRepositoryService : BaseRemotingService<OctopusCredential>, IProjectRepository, IAdminActorEvents
    {
        private readonly IAdminActor _adminProxy;
        private readonly IProjectRepositoryFactory _factory;
        private readonly IOctopusClientFactory _octopusClientFactory;


        public OctopusProjectRepositoryService(StatefulServiceContext serviceContext, string replicatorSettingsSectionName, IProjectRepositoryFactory factory, IOctopusClientFactory octopusClientFactory) : base(serviceContext, replicatorSettingsSectionName)
        {
            _factory = factory;
            _octopusClientFactory = octopusClientFactory;
            _adminProxy = ActorProxy.Create<IAdminActor>(new ActorId(nameof(OctopusDeployNuGetFeed)), FabricRuntime.GetActivationContext().ApplicationName);
        }

        public override string ServiceName => nameof(OctopusProjectRepositoryService);

        public void Decache()
        {
            _octopusClientFactory.Reset();
        }

        public Task<IEnumerable<ODataPackage>> GetAllProjectsAsync()
        {
            return _factory.GetProjectRepository(ContextObject).GetAllProjectsAsync();
        }

        public Task<bool> ExistsAsync(string projectName)
        {
            return _factory.GetProjectRepository(ContextObject).ExistsAsync(projectName);
        }

        protected override async Task OnChangeRoleAsync(ReplicaRole newRole, CancellationToken cancellationToken)
        {
            switch (newRole)
            {
                case ReplicaRole.Primary:
                    await _adminProxy.SubscribeAsync<IAdminActorEvents>(this);
                    break;
                case ReplicaRole.Unknown:
                case ReplicaRole.None:
                case ReplicaRole.IdleSecondary:
                case ReplicaRole.ActiveSecondary:
                    await _adminProxy.UnsubscribeAsync<IAdminActorEvents>(this);
                    break;
            }
        }
    }
}