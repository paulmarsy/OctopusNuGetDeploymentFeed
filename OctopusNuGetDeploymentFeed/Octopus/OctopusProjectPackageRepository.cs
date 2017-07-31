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

        public async Task<IServerPackage> GetPackageVersionAsync(string id, string version, CancellationToken token)
        {
            var project = await GetProject(id);
            if (project == null)
                return null;
            var release = await Client.Repository.Releases.FindOne(currentRelease =>
            {
                if (!SemanticVersion.TryParse(currentRelease.Version, out SemanticVersion currentReleaseSemver))
                    return false;

                return string.Equals(version, currentReleaseSemver.ToOriginalString(), StringComparison.OrdinalIgnoreCase) || string.Equals(version, currentReleaseSemver.ToNormalizedString(), StringComparison.OrdinalIgnoreCase) || string.Equals(version, currentReleaseSemver.ToFullString(), StringComparison.OrdinalIgnoreCase);
            });
            if (release == null)
                return null;

            return new OctopusServerPackage(_logger, this, release, SemanticVersion.Parse(release.Version), project, true, true);
        }

        public async Task<IEnumerable<IServerPackage>> GetPackagesAsync(string id, bool allowPrereleaseVersions, CancellationToken token)
        {
                var project = await GetProject(id);
                if (project == null)
                    return Enumerable.Empty<IServerPackage>();

                return await GetProjectReleases(project, allowPrereleaseVersions, token);
        }


        public async Task<IEnumerable<IServerPackage>> FindPackagesAsync(string searchTerm, bool allowPrereleaseVersions, CancellationToken token)
        {
                var results = new List<IServerPackage>();
                foreach (var project in await FindProject(searchTerm))
                {
                    token.ThrowIfCancellationRequested();
                    results.AddRange(await GetProjectReleases(project, allowPrereleaseVersions, token));
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
        

        private async Task<ProjectResource> GetProject(string id)
        {
            return await Client.Repository.Projects.FindByName(id);
        }

        private async Task<IEnumerable<IServerPackage>> GetProjectReleases(ProjectResource project, bool allowPrereleaseVersions, CancellationToken token)
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
                if (!string.IsNullOrWhiteSpace(version.SpecialVersion) && !allowPrereleaseVersions)
                    continue;
                results.Add(new OctopusServerPackage(_logger, this, release, version, project, isLatest, false));
                isLatest = false;
            }

            return results;
        }

        private async Task<List<ProjectResource>> FindProject(string searchTerm)
        {
            return await Client.Repository.Projects.FindMany(project => project.Name.WildcardMatch($"*{searchTerm}*"));
        }
    }
}