using Nevermore;
using NuGet;
using Octopus.Core.Model.Feed;
using Octopus.Core.Model.NuGet;
using Octopus.Core.Model.Server;
using Octopus.Core.Model.Users;
using Octopus.Server.Extensibility.Extensions;

namespace OctopusDeployNuGetFeed.Extension.Registration
{
    public class FeedRegistration : IFeedRegistration
    {
        public const string FeedId = "feeds-octopus-deploy-autodeploy-packages";
        private readonly IOctopusExtensionApiKeyService _apiKeyService;
        private readonly IRelationalStore _store;

        public FeedRegistration(IOctopusExtensionApiKeyService apiKeyService, IRelationalStore store)
        {
            _apiKeyService = apiKeyService;
            _store = store;
        }

        public void Register(string listenPrefix)
        {
            using (var transaction = _store.BeginTransaction(RetriableOperation.Select | RetriableOperation.Insert | RetriableOperation.Update, $"{nameof(FeedRegistration)}.{nameof(Register)}"))
            {
                var serverConfiguration = transaction.LoadRequired<ServerConfiguration>(ServerConfiguration.SingletonId);
                var feedUri = listenPrefix + "nuget";

                var feed = transaction.Load<Feed>(FeedId);
                if (feed == null)
                {
                    feed = new NuGetFeed(Constants.OctopusNuGetFeedName, feedUri)
                    {
                        EnhancedMode = true,
                        Username = serverConfiguration.ServerUri,
                        Password = _apiKeyService.GenerateApiKey(typeof(OctopusChainDeployNuGetFeedExtension).GetCustomAttribute<OctopusPluginAttribute>(), "OctopusDeployNuGetFeed", UserRole.ProjectDeployerRole)
                    };
                    transaction.Insert(feed, FeedId);
                }
                else if (feed.FeedUri != feedUri || feed.Username != serverConfiguration.ServerUri)
                {
                    feed.FeedUri = feedUri;
                    feed.Username = serverConfiguration.ServerUri;
                    feed.Password = _apiKeyService.GenerateApiKey(typeof(OctopusChainDeployNuGetFeedExtension).GetCustomAttribute<OctopusPluginAttribute>(), "OctopusDeployNuGetFeed", UserRole.ProjectDeployerRole);
                    transaction.Update(feed);
                }
                transaction.Commit();
            }
        }
    }
}