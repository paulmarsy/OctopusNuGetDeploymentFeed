using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.OWIN;
using OctopusDeployNuGetFeed.Services.AdminActor.Fabric;
using OctopusDeployNuGetFeed.Services.NuGetFeed.Fabric;
using OctopusDeployNuGetFeed.Services.ProjectRepository;
using OctopusDeployNuGetFeed.Services.ProjectRepository.Fabric;
using OctopusDeployNuGetFeed.Services.ReleaseRepository;
using OctopusDeployNuGetFeed.Services.ReleaseRepository.Fabric;

namespace OctopusDeployNuGetFeed.ServiceFabric
{
    public class ServiceFabricProgram : IProgram
    {
        private readonly IOctopusClientFactory _octopusClientFactory;
        private readonly OctopusProjectRepositoryFactory _octopusProjectRepositoryFactory;
        private readonly OctopusReleaseRepositoryFactory _octopusReleaseRepositoryFactory;
        private readonly IOwinStartup _startup;

        public ServiceFabricProgram(IOwinStartup startup, OctopusProjectRepositoryFactory octopusProjectRepositoryFactory, OctopusReleaseRepositoryFactory octopusReleaseRepositoryFactory, IOctopusClientFactory octopusClientFactory)
        {
            _startup = startup;
            _octopusProjectRepositoryFactory = octopusProjectRepositoryFactory;
            _octopusReleaseRepositoryFactory = octopusReleaseRepositoryFactory;
            _octopusClientFactory = octopusClientFactory;
        }

        /// <summary>
        ///     This is the entry point of the service host process.
        /// </summary>
        public async Task<int> Main(string[] args)
        {
            try
            {
                await ServiceRuntime.RegisterServiceAsync(nameof(NuGetFeedService), context => new NuGetFeedService(context, _startup));
                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(NuGetFeedService).Name);

                await ServiceRuntime.RegisterServiceAsync(nameof(OctopusProjectRepositoryService), context => new OctopusProjectRepositoryService(context, "OctopusProjectRepositoryServiceReplicatorConfig", _octopusProjectRepositoryFactory, _octopusClientFactory));
                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(OctopusProjectRepositoryService).Name);

                await ServiceRuntime.RegisterServiceAsync(nameof(OctopusReleaseRepositoryService), context => new OctopusReleaseRepositoryService(context, "OctopusReleaseRepositoryServiceReplicatorConfig", _octopusReleaseRepositoryFactory, _octopusClientFactory));
                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(OctopusReleaseRepositoryService).Name);

                await ActorRuntime.RegisterActorAsync<AdminActorService>((context, actorType) => new ActorService(context, actorType));
                ServiceEventSource.Current.ActorTypeRegistered(Process.GetCurrentProcess().Id, typeof(AdminActorService).Name);

                // Prevents this host process from terminating so services keep running.
                await Task.Delay(Timeout.Infinite);
                return 0;
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}