using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus.ProjectCache;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusProjectPackageRepositoryFactory : IServerPackageRepositoryFactory
    {
        private readonly IDictionary<(string baseUrl, string apiKey), OctopusProjectCache> _cacheRegistry = new ConcurrentDictionary<(string, string), OctopusProjectCache>();
        private readonly ILogger _logger = Startup.Logger;

        public IServerPackageRepository GetPackageRepository(IPrincipal user)
        {
            var claimsPrincipal = user as ClaimsPrincipal;
            var baseUri = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.Uri)?.Value;
            var apiKey = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.UserData)?.Value;
            _logger.Info($"GetPackageRepository baseUri: {baseUri} apiKey: {apiKey}");

            return new OctopusProjectPackageRepository(baseUri, apiKey, _logger, this);
        }

        public OctopusProjectCache GetPackageCache(IServerPackageRepository repository)
        {
            lock (_cacheRegistry)
            {
                var key = (repository.BaseUri, repository.ApiKey);
                if (!_cacheRegistry.ContainsKey(key))
                    _cacheRegistry[key] = new OctopusProjectCache(repository.BaseUri, repository.ApiKey, _logger);

                return _cacheRegistry[key];
            }
        }
    }
}