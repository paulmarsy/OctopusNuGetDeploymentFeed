using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.ServiceFabric;
using Microsoft.Owin.Hosting;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.OWIN
{
    internal class OwinCommunicationListener : ICommunicationListener
    {
        private readonly string _endpointName;
        private readonly ServiceContext _serviceContext;
        private readonly IOwinStartup _startup;
        private string _listeningAddress;
        private string _publishAddress;

        private IDisposable _webApp;

        public OwinCommunicationListener(ServiceContext serviceContext, IOwinStartup startup, string endpointName)
        {
            _serviceContext = serviceContext;
            _startup = startup;
            _endpointName = endpointName;
        }

        public Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            var serviceEndpoint = _serviceContext.CodePackageActivationContext.GetEndpoint(_endpointName);

            _listeningAddress = $"{serviceEndpoint.Protocol.ToString().ToLowerInvariant()}://+:{serviceEndpoint.Port}/";

            if (_serviceContext is StatefulServiceContext)
            {
                var statefulServiceContext = (StatefulServiceContext) _serviceContext;
                _listeningAddress = $"{_listeningAddress}{statefulServiceContext.PartitionId}/{statefulServiceContext.ReplicaId}/{Guid.NewGuid()}";
            }

            _publishAddress = _listeningAddress.Replace("+", FabricRuntime.GetNodeContext().IPAddressOrFQDN);

            try
            {
                ServiceFabricEventSource.Current.ServiceRequestStart("Starting web server on " + _listeningAddress);
                _webApp = WebApp.Start(_listeningAddress, _startup.Configuration);
                FabricTelemetryInitializerExtension.CreateFabricTelemetryInitializer(_serviceContext);
                ServiceFabricEventSource.Current.ServiceRequestStop("Listening on " + _publishAddress);
                return Task.FromResult(_publishAddress);
            }
            catch (Exception ex)
            {
                ServiceFabricEventSource.Current.ServiceHostInitializationFailed(ex.ToString());
                StopWebServer();
                throw;
            }
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            ServiceFabricEventSource.Current.ServiceRequestStop("Closing web server");
            StopWebServer();
            return Task.CompletedTask;
        }

        public void Abort()
        {
            ServiceFabricEventSource.Current.ServiceRequestStop("Aborting web server");
            StopWebServer();
        }


        private void StopWebServer()
        {
            try
            {
                _webApp?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // no-op
            }
        }
    }
}