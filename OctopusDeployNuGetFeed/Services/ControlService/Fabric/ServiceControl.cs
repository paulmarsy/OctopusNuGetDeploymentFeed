using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace OctopusDeployNuGetFeed.Services.ControlService.Fabric
{
    [StatePersistence(StatePersistence.None)]
    public class ServiceControl : Actor, IServiceControl
    {
        public ServiceControl(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        public Task Decache()
        {
            var actorEvent = GetEvent<IServiceControlEvents>();
            actorEvent.Decache();
            return Task.CompletedTask;
        }
    }
}