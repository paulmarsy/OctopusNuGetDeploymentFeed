using System.Fabric;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace OctopusDeployNuGetFeed.Services.ControlService.Remote
{
    public class RemoteServiceControlClient : IServiceControl
    {
        public async Task Decache()
        {
            await GetServiceControlProxy().Decache();
        }

        private static IServiceControl GetServiceControlProxy()
        {
            return ActorProxy.Create<IServiceControl>(new ActorId(nameof(OctopusDeployNuGetFeed)), FabricRuntime.GetActivationContext().ApplicationName);
        }
    }
}