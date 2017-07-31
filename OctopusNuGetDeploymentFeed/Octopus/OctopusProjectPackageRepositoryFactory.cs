using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusProjectPackageRepositoryFactory : IServerPackageRepositoryFactory
    {
        private readonly ILogger _logger = Startup.Logger;

        public IServerPackageRepository GetPackageRepository(IPrincipal user)
        {
            var claimsPrincipal = user as ClaimsPrincipal;
            var baseUri = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.Uri)?.Value;
            var apiKey = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.UserData)?.Value;
            _logger.Info($"GetPackageRepository baseUri: {baseUri} apiKey: {apiKey}");

            return new OctopusProjectPackageRepository(baseUri, apiKey, _logger);
        }
    }
}