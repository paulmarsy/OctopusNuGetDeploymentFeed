using System.Collections.Generic;
using System.Fabric;
using Microsoft.ApplicationInsights.ServiceFabric;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using OctopusDeployNuGetFeed.OWIN;

namespace OctopusDeployNuGetFeed.Services.NuGetFeed.Fabric
{
    /// <summary>
    ///     An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    public sealed class NuGetFeedService : StatelessService
    {
        private readonly IOwinStartup _startup;

        public NuGetFeedService(StatelessServiceContext context, IOwinStartup startup)
            : base(context)
        {
            _startup = startup;
        }

        /// <summary>
        ///     Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            FabricTelemetryInitializerExtension.SetServiceCallContext(Context);

            yield return new ServiceInstanceListener(serviceContext => new OwinCommunicationListener(serviceContext, _startup, nameof(NuGetFeedService) + "Endpoint"));
        }
    }
}