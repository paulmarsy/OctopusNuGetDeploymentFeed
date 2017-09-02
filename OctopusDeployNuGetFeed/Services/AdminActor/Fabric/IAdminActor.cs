using Microsoft.ServiceFabric.Actors;

namespace OctopusDeployNuGetFeed.Services.AdminActor.Fabric
{
    public interface IAdminActor : IActor, IActorEventPublisher<IAdminActorEvents>
    {
        void Decache();
    }
}