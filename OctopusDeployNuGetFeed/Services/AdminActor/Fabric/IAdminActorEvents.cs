using Microsoft.ServiceFabric.Actors;

namespace OctopusDeployNuGetFeed.Services.AdminActor.Fabric
{
    public interface IAdminActorEvents : IActorEvents
    {
        void Decache();
    }
}