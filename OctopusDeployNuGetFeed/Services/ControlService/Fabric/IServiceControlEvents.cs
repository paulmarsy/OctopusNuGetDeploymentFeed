using Microsoft.ServiceFabric.Actors;

namespace OctopusDeployNuGetFeed.Services.ControlService.Fabric
{
    public interface IServiceControlEvents : IActorEvents
    {
        void Decache();
    }
}