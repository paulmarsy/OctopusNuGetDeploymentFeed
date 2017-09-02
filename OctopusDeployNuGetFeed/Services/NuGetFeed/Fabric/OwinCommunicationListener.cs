using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.ServiceFabric;
using Microsoft.Owin.Hosting;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.OWIN;

namespace OctopusDeployNuGetFeed.Services.NuGetFeed.Fabric
{
    public class OwinCommunicationListener : ICommunicationListener
    {
        private readonly string _endpointName;
        private readonly ServiceContext _serviceContext;
        private readonly IOwinStartup _startup;
        private IDisposable _webApiApp;

        public OwinCommunicationListener(ServiceContext serviceContext, IOwinStartup startup, string endpointName)
        {
            _startup = startup;
            _endpointName = endpointName;
            _serviceContext = serviceContext;
        }

        public Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            var serviceEndpoint = _serviceContext.CodePackageActivationContext.GetEndpoint(_endpointName);

            var listeningAddress = $"{serviceEndpoint.Protocol}://+:{serviceEndpoint.Port}/";
            _webApiApp = WebApp.Start(listeningAddress, appBuilder => _startup.Configuration(appBuilder));
            FabricTelemetryInitializerExtension.CreateFabricTelemetryInitializer(_serviceContext);

            var publishAddress = listeningAddress.Replace("+", FabricRuntime.GetNodeContext().IPAddressOrFQDN);
            ServiceFabricEventSource.Current.Info($"Listening on {publishAddress}");

            return Task.FromResult(publishAddress);
        }

        public void Abort()
        {
            StopWebServer();
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            StopWebServer();
            return Task.FromResult(true);
        }

        private void StopWebServer()
        {
            try
            {
                _webApiApp?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}