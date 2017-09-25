using System.Linq;
using System.Net;
using System.Threading;
using Nevermore;
using NuGet;
using Octopus.Client.Exceptions;
using Octopus.Core.Model.Users;
using Octopus.Data.Model.User;
using Octopus.Server.Extensibility.Authentication.HostServices;
using Octopus.Server.Extensibility.Extensions;

namespace OctopusDeployNuGetFeed.Extension.Registration
{
    public class OctopusExtensionApiKeyService : IOctopusExtensionApiKeyService
    {
        public static readonly string OctopusExtensionsTeamId = "teams-octopusextensions";
        private readonly IRelationalStore _store;
        private readonly IUpdateableUserStore _userStore;

        public OctopusExtensionApiKeyService(IRelationalStore store, IUpdateableUserStore userStore)
        {
            _store = store;
            _userStore = userStore;
        }

        public string GenerateApiKey(IOctopusExtensionMetadata extension, string userId, params string[] requiredRoles)
        {
            var user = GetOrCreateServiceUser(extension, userId);
            UpdateServiceUserTeam(user, requiredRoles);

            using (var transaction = _store.BeginTransaction(RetriableOperation.Insert, $"{nameof(OctopusExtensionApiKeyService)}.{nameof(GenerateApiKey)}"))
            {
                var apiKeyInstance = ApiKey.GenerateFor(user.Id, $"Extension: {extension.FriendlyName} by {extension.Author}", out var apiKey);
                transaction.Insert(apiKeyInstance, apiKeyInstance.Id);
                transaction.Commit();

                return apiKey;
            }
        }

        private void UpdateServiceUserTeam(IUser user, string[] roles)
        {
            using (var transaction = _store.BeginTransaction(RetriableOperation.Select | RetriableOperation.Insert | RetriableOperation.Update, $"{nameof(OctopusExtensionApiKeyService)}.{nameof(UpdateServiceUserTeam)}"))
            {
                var team = transaction.Load<Team>(OctopusExtensionsTeamId);
                if (team == null)
                {
                    team = new Team("Octopus Extensions");
                    team.MemberUserIds.Add(user.Id);
                    team.UserRoleIds.AddRange(roles);
                    transaction.Insert(team, OctopusExtensionsTeamId);
                }
                else
                {
                    if (!team.MemberUserIds.Contains(user.Id))
                        team.MemberUserIds.Add(user.Id);
                    foreach (var role in roles.Where(r => !team.UserRoleIds.Contains(r)))
                        team.UserRoleIds.Add(role);
                    transaction.Update(team);
                }
                transaction.Commit();
            }
        }

        private IUser GetOrCreateServiceUser(IOctopusExtensionMetadata extension, string id)
        {
            var serviceUser = _userStore.GetByUsername(id);
            if (serviceUser != null)
                return serviceUser;

            var userResult = _userStore.Create(id, extension.FriendlyName, string.Empty, CancellationToken.None, isService: true);
            if (!userResult.Succeeded)
                throw new OctopusSecurityException((int) HttpStatusCode.NotModified, userResult.FailureReason);

            return userResult.User;
        }
    }
}