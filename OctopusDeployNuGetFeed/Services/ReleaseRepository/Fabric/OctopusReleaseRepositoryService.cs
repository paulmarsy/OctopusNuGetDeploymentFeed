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

namespace OctopusDeployNuGetFeed.Services.ReleaseRepository.Fabric
{
    public class OctopusReleaseRepositoryService : BaseRemotingService<OctopusCredential>, IReleaseRepository, IServiceControlEvents
    {
        private readonly IAppInsights _appInsights;
        private readonly IReleaseRepositoryFactory _factory;
        private readonly IOctopusClientFactory _octopusClientFactory;
        private readonly IServiceControl _serviceControlActor;

        public OctopusReleaseRepositoryService(StatefulServiceContext context, string replicatorSettingsSectionName, OctopusReleaseRepositoryFactory factory, IOctopusClientFactory octopusClientFactory, IAppInsights appInsights) : base(context, replicatorSettingsSectionName)
        {
            _factory = factory;
            _octopusClientFactory = octopusClientFactory;
            _appInsights = appInsights;
            _serviceControlActor = ActorProxy.Create<IServiceControl>(new ActorId(nameof(OctopusDeployNuGetFeed)), FabricRuntime.GetActivationContext().ApplicationName);
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