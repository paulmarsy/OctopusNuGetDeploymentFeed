using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Server.Core.Infrastructure;
using NuGet.Server.Core.Logging;
using Octopus.Client;
using Octopus.Client.Model;

namespace NuGet.Server.V2.Samples.OwinHost.Octopus
{
    public class OctopusProjectPackageRepositoryFactory : IServerPackageRepositoryFactory
    {
       private const string ApiKeyHeader = "X-NUGET-APIKEY";

        private readonly Core.Logging.ILogger _logger;

        public OctopusProjectPackageRepositoryFactory(Core.Logging.ILogger logger)
        {
            _logger = logger;
        }
        public IServerPackageRepository GetPackageRepository(IPrincipal user)
        {
            var claimsPrincipal = user as ClaimsPrincipal;
            var baseUri = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.Uri)?.Value;
            var apiKey = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.UserData)?.Value;
            _logger.Log(LogLevel.Info, $"GetPackageRepository baseUri: {baseUri}, apiKey: {apiKey}");

            return new OctopusProjectPackageRepository(baseUri, apiKey, _logger);
        }
    }
    public class OctopusProjectPackageRepository : IServerPackageRepository
    {
        public string BaseUri { get; }
        public string ApiKey { get; }
        private readonly Core.Logging.ILogger _logger;

        public OctopusProjectPackageRepository(string baseUri, string apiKey, Core.Logging.ILogger logger)
        {
            BaseUri = baseUri;
            ApiKey = apiKey;
            _logger = logger;
        }

        private OctopusServerEndpoint _endpoint;
        private OctopusServerEndpoint Endpoint => _endpoint ?? (_endpoint = new OctopusServerEndpoint(BaseUri, ApiKey));
        

        public async Task<IEnumerable<IServerPackage>> GetPackagesAsync(string id, string version, CancellationToken token)
        {
            try
            {
                _logger.Log(LogLevel.Info, $"GetPackagesAsync called,id:{id}, version:{version}");
                var results = new List<IServerPackage>();
                using (var client = await OctopusAsyncClient.Create(_endpoint))
                {
                    var project = await client.Repository.Projects.FindByName(id);
                    if (string.IsNullOrWhiteSpace(version))
                    {
                        var first = true;
                        foreach (var release in (await client.Repository.Projects.GetReleases(project)).Items)
                        {
                            if (!SemanticVersion.TryParse(release.Version, out SemanticVersion semver))
                            {
                                _logger.Log(LogLevel.Warning, $"GetPackagesAsync.Version.TryParse: {project.Name} ({project.Id}) {release.Version}");
                                continue;
                            }
                            results.Add(new OctopusServerPackage(_logger,this, release, semver, project, first));
                            first = false;
                        }
                    }
                    else
                    {
                        var release = await client.Repository.Projects.GetReleaseByVersion(project, version);
                        if (!SemanticVersion.TryParse(release.Version, out SemanticVersion semver))
                        {
                            _logger.Log(LogLevel.Warning, $"GetPackagesAsync.Version.TryParse: {project.Name} ({project.Id}) {release.Version}");
                        }
                        else
                        {
                            results.Add(new OctopusServerPackage(_logger, this,release, semver, project, true));
                        }

                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.Log(
                    LogLevel.Error,
                    "Error in GetPackagesAsync: {0} {1}",
                    ex.Message,
                    ex.StackTrace);

                throw;
            }
        }

 
        public async Task<IEnumerable<IServerPackage>> SearchAsync(string searchTerm, bool allowPrereleaseVersions, CancellationToken token)
        {
        try
            {
                _logger.Log(LogLevel.Info, $"SearchAsync called, searchTerm: {searchTerm}, allowPrereleaseVersions: {allowPrereleaseVersions}");

                var results = new List<IServerPackage>();
                using (var client = await OctopusAsyncClient.Create(_endpoint))
                {
                    foreach (var project in await client.Repository.Projects.FindMany(project => project.Name.WildcardMatch($"*{searchTerm}*")))
                    {
                        var first = true;
                        foreach (var release in (await client.Repository.Projects.GetReleases(project)).Items)
                        {
                            if (!SemanticVersion.TryParse(release.Version, out SemanticVersion version))
                            {
                                _logger.Log(LogLevel.Warning, $"SearchAsync.Version.TryParse: {project.Name} ({project.Id}) {release.Version}");
                                continue;
                            }
                            if (!string.IsNullOrWhiteSpace(version.SpecialVersion) && !allowPrereleaseVersions)
                                continue;
                            results.Add(new OctopusServerPackage(_logger,this, release, version, project, first));
                            first = false;
                        }
                    }

                }
                return results;
            }
            catch (Exception ex)
            {
                _logger.Log(
                    LogLevel.Error,
                    "Error in SearchAsync: {0} {1}",
                    ex.Message,
                    ex.StackTrace);

                throw;
            }
        }

        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(BaseUri) || string.IsNullOrWhiteSpace(ApiKey))
                        return false;
                    
                    return Endpoint != null;
                }
                catch
                {
                    return false;
                }
            }
    }

        public Task ClearCacheAsync(CancellationToken token)
        {
            _logger.Log(LogLevel.Warning, "ClearCacheAsync called");
            return Task.CompletedTask;
        }

        public Task RemovePackageAsync(string packageId, SemanticVersion version, CancellationToken token)
        {
            _logger.Log(LogLevel.Warning, "RemovePackageAsync called");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}