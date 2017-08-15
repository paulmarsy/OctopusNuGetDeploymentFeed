using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusPackageRepositoryFactory : IPackageRepositoryFactory
    {
        private readonly IAppInsights _appInsights;
        private readonly ILogger _logger;
        private readonly IDictionary<(string baseUrl, string apiKey), IPackageRepository> _repositories = new ConcurrentDictionary<(string, string), IPackageRepository>();

        public OctopusPackageRepositoryFactory(ILogger logger, IAppInsights appInsights)
        {
            _logger = logger;
            _appInsights = appInsights;
        }

        public IPackageRepository GetPackageRepository(IPrincipal user)
        {
            var context = GetOctopusContext(user);

            if (!_repositories.ContainsKey(context))
                lock (_repositories)
                {
                    CreateOctopusRepository(context);
                }

            return _repositories[context];
        }

        private static (string baseUrl, string apiKey) GetOctopusContext(IPrincipal user)
        {
            var claimsPrincipal = user as ClaimsPrincipal;
            var baseUri = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.Uri)?.Value;
            var apiKey = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.UserData)?.Value;

            return (baseUri, apiKey);
        }

        public void CreateOctopusRepository((string baseUrl, string apiKey) context)
        {
            if (_repositories.ContainsKey(context))
                return;

            var server = new OctopusServer(_appInsights, context.baseUrl, context.apiKey);

            var authenticated = server.IsAuthenticated;
            _logger.Info($"Creating Octopus API Connection: {server.BaseUri}. IsAuthenticated: {authenticated}");
            _appInsights.TrackEvent("CreateOctopusRepository", new Dictionary<string, string>
            {
                {"BaseUri", server.BaseUri},
                {"IsAuthenticated", authenticated.ToString()}
            });
            if (!server.IsAuthenticated)
                return;

            server.ConfigureAppInsightsDependencyTracking();

            var cache = new OctopusCache(server, _logger);
            var repository = new OctopusPackageRepository(_logger, server, cache);
            _repositories[context] = repository;
        }
    }
}