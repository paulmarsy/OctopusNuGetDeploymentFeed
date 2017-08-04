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
        private readonly IDictionary<(string baseUrl, string apiKey), IPackageRepository> _repositories = new ConcurrentDictionary<(string, string), IPackageRepository>();
        private readonly ILogger _logger = Startup.Logger;

        public IPackageRepository GetPackageRepository(IPrincipal user)
        {
            var context = GetOctopusContext(user);

            if (!_repositories.ContainsKey(context))
            {
                lock (_repositories)
                {
                    CreateOctopusRepository(context);
                }
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

                var server = new OctopusServer(context.baseUrl, context.apiKey);

            var authenticated = server.IsAuthenticated;
            _logger.Info($"Creating Octopus API Connection: {server.BaseUri}. IsAuthenticated: {authenticated}");
            if (!server.IsAuthenticated)
                return;
      
            var cache = new OctopusCache(_logger, server);
            var repository = new OctopusPackageRepository(_logger, server, cache);
            _repositories[context] = repository;
        }
    }
}