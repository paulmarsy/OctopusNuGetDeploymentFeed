using System;
using System.Fabric;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Text;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Client;

namespace OctopusDeployNuGetFeed.Remoting
{
    public abstract class BaseRemotingClient<TService, TContext> where TService : IService
    {
        private readonly IServiceProxyFactory _serviceProxyFactory;

        protected BaseRemotingClient()
        {
            _serviceProxyFactory = new ServiceProxyFactory(c => new ContextServiceRemotingClientFactory(new FabricTransportServiceRemotingClientFactory(callbackClient: c), ContextName));
        }

        protected virtual string ContextName => ServiceName;

        protected abstract TContext ContextObject { get; }
        public abstract string ServiceName { get; }
        public virtual TargetReplicaSelector TargetReplica => TargetReplicaSelector.Default;

        protected TService GetProxy(string partitionKey)
        {
            CallContext.LogicalSetData(ContextName, ContextObject);
            return _serviceProxyFactory.CreateServiceProxy<TService>(new Uri($"{FabricRuntime.GetActivationContext().ApplicationName}/{ServiceName}"), GetPartitionKey(partitionKey), TargetReplica, ServiceName);
        }

        private static ServicePartitionKey GetPartitionKey(string partitionKey)
        {
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(partitionKey));
                return new ServicePartitionKey(BitConverter.ToInt64(bytes, 0));
            }
        }
    }
}