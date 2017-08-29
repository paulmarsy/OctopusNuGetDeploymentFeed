using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace OctopusDeployNuGetFeed.Remoting
{
    public class ContextServiceRemotingClientFactory : IServiceRemotingClientFactory
    {
        private readonly string _callContextDataName;

        private readonly IServiceRemotingClientFactory _inner;

        public ContextServiceRemotingClientFactory(IServiceRemotingClientFactory inner, string callContextDataName)
        {
            _inner = inner;
            _callContextDataName = callContextDataName;
        }


        async Task<IServiceRemotingClient> ICommunicationClientFactory<IServiceRemotingClient>.GetClientAsync(Uri serviceUri, ServicePartitionKey partitionKey, TargetReplicaSelector targetReplicaSelector, string listenerName, OperationRetrySettings retrySettings, CancellationToken cancellationToken)
        {
            var client = await _inner.GetClientAsync(serviceUri, partitionKey, targetReplicaSelector, listenerName, retrySettings, cancellationToken);
            return new ContextServiceRemotingClient(client, _callContextDataName);
        }

        async Task<IServiceRemotingClient> ICommunicationClientFactory<IServiceRemotingClient>.GetClientAsync(ResolvedServicePartition previousRsp, TargetReplicaSelector targetReplicaSelector, string listenerName, OperationRetrySettings retrySettings, CancellationToken cancellationToken)
        {
            var client = await _inner.GetClientAsync(previousRsp, targetReplicaSelector, listenerName, retrySettings, cancellationToken);
            return new ContextServiceRemotingClient(client, _callContextDataName);
        }

        Task<OperationRetryControl> ICommunicationClientFactory<IServiceRemotingClient>.ReportOperationExceptionAsync(IServiceRemotingClient client, ExceptionInformation exceptionInformation, OperationRetrySettings retrySettings, CancellationToken cancellationToken)
        {
            var innerClient = client as ContextServiceRemotingClient;

            return _inner.ReportOperationExceptionAsync(innerClient?.WrappedClient ?? client, exceptionInformation, retrySettings, cancellationToken);
        }

        event EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> ICommunicationClientFactory<IServiceRemotingClient>.ClientConnected
        {
            add => _inner.ClientConnected += value;
            remove => _inner.ClientConnected -= value;
        }

        event EventHandler<CommunicationClientEventArgs<IServiceRemotingClient>> ICommunicationClientFactory<IServiceRemotingClient>.ClientDisconnected
        {
            add => _inner.ClientDisconnected += value;
            remove => _inner.ClientDisconnected -= value;
        }
    }
}