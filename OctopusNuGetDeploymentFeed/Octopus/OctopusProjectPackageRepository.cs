using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Logging;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusProjectPackageRepository : IServerPackageRepository
    {
        private readonly ILogger _logger;
        private IOctopusAsyncClient _client;

        private OctopusServerEndpoint _endpoint;

        public OctopusProjectPackageRepository(string baseUri, string apiKey, ILogger logger)
        {
            BaseUri = baseUri;
            ApiKey = apiKey;
            _logger = logger;
        }

        private OctopusServerEndpoint Endpoint => _endpoint ?? (_endpoint = new OctopusServerEndpoint(BaseUri, ApiKey));
        internal IOctopusAsyncClient Client => _client ?? (_client = OctopusAsyncClient.Create(Endpoint).GetAwaiter().GetResult());
        public string BaseUri { get; }
        public string ApiKey { get; }

        public async Task<IServerPackage> GetProjectReleaseAsync(string id, string version, CancellationToken token)
        {
            if (!SemanticVersion.TryParse(version, out SemanticVersion semver))
            {
                _logger.Warning($"GetProjectReleaseAsync.SemanticVersion.TryParse: {id} {version}");
                return null;
            }

            var project = await Client.Repository.Projects.FindByName(id);
            if (project == null)
                return null;

            return (await GetProjectReleases(project, true, token)).SingleOrDefault(package => semver.Equals(package.Version) ||
                                                                                         string.Equals(version, package.Version.ToOriginalString(), StringComparison.OrdinalIgnoreCase) ||
                                                                                         string.Equals(version, package.Version.ToNormalizedString(), StringComparison.OrdinalIgnoreCase) ||
                                                                                         string.Equals(version, package.Version.ToFullString(), StringComparison.OrdinalIgnoreCase));
        }

        public async Task<IEnumerable<IServerPackage>> GetProjectReleasesAsync(string id, CancellationToken token)
        {
            var project = await Client.Repository.Projects.FindByName(id);
            if (project == null)
                return Enumerable.Empty<IServerPackage>();

            return await GetProjectReleases(project, false, token);
        }
        
        public async Task<IEnumerable<IServerPackage>> FindProjectsAsync(string searchTerm, CancellationToken token)
        {
            var results = new List<IServerPackage>();
            foreach (var project in await Client.Repository.Projects.FindMany(project => project.Name.WildcardMatch($"*{searchTerm}*")))
            {
                token.ThrowIfCancellationRequested();
                results.AddRange(await GetProjectReleases(project, false, token));
            }
            return results;
        }

        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(BaseUri) || string.IsNullOrWhiteSpace(ApiKey))
                        return false;

                    return Endpoint != null && Client != null;
                }
                catch
                {
                    return false;
                }
            }
        }
        
        private async Task<IEnumerable<IServerPackage>> GetProjectReleases(ProjectResource project, bool detailedView, CancellationToken token)
        {
            var results = new List<IServerPackage>();

            var isLatest = true;
            foreach (var release in (await Client.Repository.Projects.GetReleases(project)).Items)
            {
                token.ThrowIfCancellationRequested();
                if (!SemanticVersion.TryParse(release.Version, out SemanticVersion version))
                {
                    _logger.Warning($"GetProjectReleases.SemanticVersion.TryParse: {project.Name} ({project.Id}) {release.Version}");
                    continue;
                }
                results.Add(new OctopusServerPackage(_logger, this, release, version, project, isLatest, detailedView));
                isLatest = false;
            }

            return results;
        }
    }
}