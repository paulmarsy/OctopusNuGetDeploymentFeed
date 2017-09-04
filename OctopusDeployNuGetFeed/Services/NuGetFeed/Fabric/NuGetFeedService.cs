using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.ServiceFabric;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.OWIN;

namespace OctopusDeployNuGetFeed.Services.NuGetFeed.Fabric
{
    /// <summary>
    ///     An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    public sealed class NuGetFeedService : StatelessService
    {
        private readonly IAppInsights _appInsights;
        private readonly IOwinStartup _startup;

        public NuGetFeedService(StatelessServiceContext context, IOwinStartup startup, IAppInsights appInsights)
            : base(context)
        {
            _startup = startup;
            _appInsights = appInsights;
        }

        protected override Task RunAsync(CancellationToken cancellationToken)
        {
            _appInsights.SetCloudContext(Context);
            return base.RunAsync(cancellationToken);
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