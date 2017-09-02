using System.Fabric;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace OctopusDeployNuGetFeed.Services.AdminActor.Fabric
{
    public class RemoteAdminServiceClient : IAdminService
    {
        public void Decache()
        {
            var adminActorProxy = ActorProxy.Create<IAdminActor>(new ActorId(nameof(OctopusDeployNuGetFeed)), FabricRuntime.GetActivationContext().ApplicationName);
            adminActorProxy.Decache();
        }
    }
}