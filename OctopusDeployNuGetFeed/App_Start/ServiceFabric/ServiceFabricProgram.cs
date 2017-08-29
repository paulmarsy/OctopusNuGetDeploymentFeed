using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Runtime;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.OWIN;
using OctopusDeployNuGetFeed.Services.NuGetFeed.Fabric;
using OctopusDeployNuGetFeed.Services.ProjectRepository;
using OctopusDeployNuGetFeed.Services.ProjectRepository.Fabric;
using OctopusDeployNuGetFeed.Services.ReleaseRepository;
using OctopusDeployNuGetFeed.Services.ReleaseRepository.Fabric;

namespace OctopusDeployNuGetFeed.ServiceFabric
{
    public class ServiceFabricProgram : IProgram
    {
        private readonly OctopusProjectRepositoryFactory _octopusProjectRepositoryFactory;
        private readonly OctopusReleaseRepositoryFactory _octopusReleaseRepositoryFactory;
        private readonly IOwinStartup _startup;

        public ServiceFabricProgram(IOwinStartup startup, OctopusProjectRepositoryFactory octopusProjectRepositoryFactory, OctopusReleaseRepositoryFactory octopusReleaseRepositoryFactory)
        {
            _startup = startup;
            _octopusProjectRepositoryFactory = octopusProjectRepositoryFactory;
            _octopusReleaseRepositoryFactory = octopusReleaseRepositoryFactory;
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

                await ServiceRuntime.RegisterServiceAsync(nameof(OctopusProjectRepositoryService), context => new OctopusProjectRepositoryService(context, "OctopusProjectRepositoryServiceReplicatorConfig", _octopusProjectRepositoryFactory));
                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(OctopusProjectRepositoryService).Name);

                await ServiceRuntime.RegisterServiceAsync(nameof(OctopusReleaseRepositoryService), context => new OctopusReleaseRepositoryService(context, "OctopusReleaseRepositoryServiceReplicatorConfig", _octopusReleaseRepositoryFactory));
                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(OctopusReleaseRepositoryService).Name);

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