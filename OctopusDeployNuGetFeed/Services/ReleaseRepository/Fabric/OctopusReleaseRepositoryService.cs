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

namespace OctopusDeployNuGetFeed.Services.ReleaseRepository.Fabric
{
    public class OctopusReleaseRepositoryService : BaseRemotingService<OctopusCredential>, IReleaseRepository, IAdminActorEvents
    {
        private readonly IAdminActor _adminProxy;
        private readonly IReleaseRepositoryFactory _factory;
        private readonly IOctopusClientFactory _octopusClientFactory;

        public OctopusReleaseRepositoryService(StatefulServiceContext context, string replicatorSettingsSectionName, OctopusReleaseRepositoryFactory factory, IOctopusClientFactory octopusClientFactory) : base(context, replicatorSettingsSectionName)
        {
            _factory = factory;
            _octopusClientFactory = octopusClientFactory;
            _adminProxy = ActorProxy.Create<IAdminActor>(new ActorId(nameof(OctopusDeployNuGetFeed)), FabricRuntime.GetActivationContext().ApplicationName);
        }

        public override string ServiceName => nameof(OctopusReleaseRepositoryService);

        public void Decache()
        {
            _octopusClientFactory.Reset();
        }

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