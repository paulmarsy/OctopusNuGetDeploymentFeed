using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.OWIN;
using OctopusDeployNuGetFeed.Services.ControlService.Fabric;
using OctopusDeployNuGetFeed.Services.NuGetFeed.Fabric;
using OctopusDeployNuGetFeed.Services.ProjectRepository;
using OctopusDeployNuGetFeed.Services.ProjectRepository.Fabric;
using OctopusDeployNuGetFeed.Services.ReleaseRepository;
using OctopusDeployNuGetFeed.Services.ReleaseRepository.Fabric;
using OctopusDeployNuGetFeed.Services.Watchdog;

namespace OctopusDeployNuGetFeed.ServiceFabric
{
    public class ServiceFabricProgram : IProgram
    {
        private readonly IOctopusClientFactory _octopusClientFactory;
        private readonly IAppInsights _appInsights;
        private readonly OctopusProjectRepositoryFactory _octopusProjectRepositoryFactory;
        private readonly OctopusReleaseRepositoryFactory _octopusReleaseRepositoryFactory;
        private readonly IOwinStartup _startup;

        public ServiceFabricProgram(IOwinStartup startup, OctopusProjectRepositoryFactory octopusProjectRepositoryFactory, OctopusReleaseRepositoryFactory octopusReleaseRepositoryFactory, IOctopusClientFactory octopusClientFactory, IAppInsights appInsights)
        {
            _startup = startup;
            _octopusProjectRepositoryFactory = octopusProjectRepositoryFactory;
            _octopusReleaseRepositoryFactory = octopusReleaseRepositoryFactory;
            _octopusClientFactory = octopusClientFactory;
            _appInsights = appInsights;
        }

        /// <summary>
        ///     This is the entry point of the service host process.
        /// </summary>
        public async Task Main(string[] args)
        {
            try
            {
                await ServiceRuntime.RegisterServiceAsync(nameof(NuGetFeedService), context => new NuGetFeedService(context, _startup, _appInsights));
                ServiceFabricEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(NuGetFeedService).Name);

                await ServiceRuntime.RegisterServiceAsync(nameof(OctopusProjectRepositoryService), context => new OctopusProjectRepositoryService(context, "OctopusProjectRepositoryServiceReplicatorConfig", _octopusProjectRepositoryFactory, _octopusClientFactory, _appInsights));
                ServiceFabricEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(OctopusProjectRepositoryService).Name);

                await ServiceRuntime.RegisterServiceAsync(nameof(OctopusReleaseRepositoryService), context => new OctopusReleaseRepositoryService(context, "OctopusReleaseRepositoryServiceReplicatorConfig", _octopusReleaseRepositoryFactory, _octopusClientFactory, _appInsights));
                ServiceFabricEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(OctopusReleaseRepositoryService).Name);

                await ActorRuntime.RegisterActorAsync<ServiceControl>((context, actorType) => new ActorService(context, actorType));
                ServiceFabricEventSource.Current.ActorTypeRegistered(Process.GetCurrentProcess().Id, typeof(ServiceControl).Name);

                await ServiceRuntime.RegisterServiceAsync(nameof(WatchdogService), context => new WatchdogService(context, _appInsights));
                ServiceFabricEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(WatchdogService).Name);

                // Prevents this host process from terminating so services keep running.
                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ServiceFabricEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}