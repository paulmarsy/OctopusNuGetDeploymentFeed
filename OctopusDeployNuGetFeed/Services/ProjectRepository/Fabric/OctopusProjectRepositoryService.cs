using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.Remoting;
using OctopusDeployNuGetFeed.Services.ControlService;
using OctopusDeployNuGetFeed.Services.ControlService.Fabric;

namespace OctopusDeployNuGetFeed.Services.ProjectRepository.Fabric
{
    public class OctopusProjectRepositoryService : BaseRemotingService<OctopusCredential>, IProjectRepository, IServiceControlEvents
    {
        private readonly IProjectRepositoryFactory _factory;
        private readonly IOctopusClientFactory _octopusClientFactory;
        private readonly IAppInsights _appInsights;
        private readonly IServiceControl _serviceControlActor;


        public OctopusProjectRepositoryService(StatefulServiceContext serviceContext, string replicatorSettingsSectionName, IProjectRepositoryFactory factory, IOctopusClientFactory octopusClientFactory, IAppInsights appInsights) : base(serviceContext, replicatorSettingsSectionName)
        {
            _factory = factory;
            _octopusClientFactory = octopusClientFactory;
            _appInsights = appInsights;
            _serviceControlActor = ActorProxy.Create<IServiceControl>(new ActorId(nameof(OctopusDeployNuGetFeed)), FabricRuntime.GetActivationContext().ApplicationName);
        }

        public override string ServiceName => nameof(OctopusProjectRepositoryService);

        public Task<IEnumerable<ODataPackage>> GetAllProjectsAsync()
        {
            return _factory.GetProjectRepository(ContextObject).GetAllProjectsAsync();
        }

        public Task<bool> ExistsAsync(string projectName)
        {
            return _factory.GetProjectRepository(ContextObject).ExistsAsync(projectName);
        }

        public void Decache()
        {
            _octopusClientFactory.Decache().Wait();
        }

        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            _appInsights.SetCloudContext(Context);
            return base.RunAsync(cancellationToken);
        }

        protected override async Task OnChangeRoleAsync(ReplicaRole newRole, CancellationToken cancellationToken)
        {
            switch (newRole)
            {
                case ReplicaRole.Primary:
                    await _serviceControlActor.SubscribeAsync<IServiceControlEvents>(this);
                    break;
                case ReplicaRole.Unknown:
                case ReplicaRole.None:
                case ReplicaRole.IdleSecondary:
                case ReplicaRole.ActiveSecondary:
                    await _serviceControlActor.UnsubscribeAsync<IServiceControlEvents>(this);
                    break;
            }
        }
        
    }
}