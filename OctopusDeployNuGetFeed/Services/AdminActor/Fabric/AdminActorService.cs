﻿using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace OctopusDeployNuGetFeed.Services.AdminActor.Fabric
{
    /// <remarks>
    ///     This class represents an actor.
    ///     Every ActorID maps to an instance of this class.
    ///     The StatePersistence attribute determines persistence and replication of actor state:
    ///     - Persisted: State is written to disk and replicated.
    ///     - Volatile: State is kept in memory only and replicated.
    ///     - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Volatile)]
    public class AdminActorService : Actor, IAdminActor
    {
        /// <summary>
        ///     Initializes a new instance of AdminActorService
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public AdminActorService(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        public void Decache()
        {
            var actorEvent = GetEvent<IAdminActorEvents>();
            actorEvent.Decache();
        }
    }
}