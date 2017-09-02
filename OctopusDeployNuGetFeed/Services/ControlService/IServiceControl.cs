using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using OctopusDeployNuGetFeed.Services.ControlService.Fabric;

namespace OctopusDeployNuGetFeed.Services.ControlService
{
    public interface IServiceControl : IActor, IActorEventPublisher<IServiceControlEvents>
    {
        Task Decache();
    }
}