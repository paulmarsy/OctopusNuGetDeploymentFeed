using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using Microsoft.ApplicationInsights.ServiceFabric;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace OctopusDeployNuGetFeed.Remoting
{
    public abstract class BaseRemotingService<TContext> : StatefulService, IService where TContext : class
    {
        private readonly AsyncLocal<TContext> _asyncLocalContext = new AsyncLocal<TContext>();

        protected BaseRemotingService(StatefulServiceContext serviceContext, string replicatorSettingsSectionName) :
            base(serviceContext, new ReliableStateManager(serviceContext,
                new ReliableStateManagerConfiguration(replicatorSettingsSectionName: replicatorSettingsSectionName)))
        {
        }

        protected virtual string ContextName => ServiceName;
        protected TContext ContextObject => _asyncLocalContext.Value;
        public virtual string ServiceEndpointName => ServiceName + "Endpoint";
        public abstract string ServiceName { get; }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            FabricTelemetryInitializerExtension.SetServiceCallContext(Context);

            yield return new ServiceReplicaListener(context => new FabricTransportServiceRemotingListener(context, new ContextServiceRemotingDispatcher<TContext>(context, this, ContextName, contextObject => _asyncLocalContext.Value = contextObject), new FabricTransportRemotingListenerSettings
            {
                EndpointResourceName = ServiceEndpointName
            }), ServiceName);
        }
    }
}