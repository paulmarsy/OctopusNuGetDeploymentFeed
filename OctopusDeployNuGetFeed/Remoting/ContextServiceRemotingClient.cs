using System.Fabric;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace OctopusDeployNuGetFeed.Remoting
{
    public class ContextServiceRemotingClient : IServiceRemotingClient
    {
        private readonly string _callContextDataName;

        public ContextServiceRemotingClient(IServiceRemotingClient wrappedClient, string callContextDataName)
        {
            WrappedClient = wrappedClient;
            _callContextDataName = callContextDataName;
        }

        public IServiceRemotingClient WrappedClient { get; }

        ResolvedServicePartition ICommunicationClient.ResolvedServicePartition
        {
            get => WrappedClient.ResolvedServicePartition;
            set => WrappedClient.ResolvedServicePartition = value;
        }

        string ICommunicationClient.ListenerName
        {
            get => WrappedClient.ListenerName;
            set => WrappedClient.ListenerName = value;
        }

        ResolvedServiceEndpoint ICommunicationClient.Endpoint
        {
            get => WrappedClient.Endpoint;
            set => WrappedClient.Endpoint = value;
        }

        Task<byte[]> IServiceRemotingClient.RequestResponseAsync(ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            messageHeaders.AddHeader(_callContextDataName, JsonBufferSerializer.Serialize(CallContext.LogicalGetData(_callContextDataName)));

            return WrappedClient.RequestResponseAsync(messageHeaders, requestBody);
        }

        void IServiceRemotingClient.SendOneWay(ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            messageHeaders.AddHeader(_callContextDataName, JsonBufferSerializer.Serialize(CallContext.LogicalGetData(_callContextDataName)));

            WrappedClient.SendOneWay(messageHeaders, requestBody);
        }
    }
}