using System;
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;

namespace OctopusDeployNuGetFeed.Remoting
{
    public class ContextServiceRemotingDispatcher<TContext> : ServiceRemotingDispatcher
    {
        private readonly string _callContextDataName;
        private readonly Action<TContext> _contextObjectSetter;

        public ContextServiceRemotingDispatcher(ServiceContext serviceContext, IService service, string callContextDataName, Action<TContext> contextObjectSetter) : base(serviceContext, service)
        {
            _callContextDataName = callContextDataName;
            _contextObjectSetter = contextObjectSetter;
        }

        public override void HandleOneWay(IServiceRemotingRequestContext requestContext, ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            if (messageHeaders.TryGetHeaderValue(_callContextDataName, out byte[] buffer))
                _contextObjectSetter(JsonBufferSerializer.Deserialize<TContext>(buffer));

            base.HandleOneWay(requestContext, messageHeaders, requestBody);
        }

        public override Task<byte[]> RequestResponseAsync(IServiceRemotingRequestContext requestContext, ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            if (messageHeaders.TryGetHeaderValue(_callContextDataName, out byte[] buffer))
                _contextObjectSetter(JsonBufferSerializer.Deserialize<TContext>(buffer));

            return base.RequestResponseAsync(requestContext, messageHeaders, requestBody);
        }
    }
}