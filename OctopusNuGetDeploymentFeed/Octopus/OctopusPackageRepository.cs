using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusPackageRepository : IPackageRepository
    {
        private readonly ILogger _logger;
        private readonly IOctopusCache _octopusCache;
        private readonly IOctopusServer _server;

        public OctopusPackageRepository(ILogger logger, IOctopusServer server, IOctopusCache octopusCache)
        {
            _logger = logger;
            _server = server;
            _octopusCache = octopusCache;
        }

        public IDownloadableNuGetPackage GetOctopusReleasePackage(string name, string version, CancellationToken token)
        {
            var semver = version.ToSemanticVersion();
            if (semver == null)
                return null;

            var project = _octopusCache.GetProject(name);
            if (project == null)
                return null;

            var release = _octopusCache.GetRelease(project, semver);
            if (release == null)
                return null;

            var channel = _octopusCache.GetChannel(release.ChannelId);
            if (channel == null)
                return null;

            return new ReleasePackage(_logger, _server, _octopusCache, project, release, channel);
        }

        public IEnumerable<INuGetPackage> FindOctopusReleasePackages(string name, CancellationToken token)
        {
            var project = _octopusCache.GetProject(name);
            if (project == null)
                yield break;

            var isLatest = true;
            foreach (var release in _octopusCache.ListReleases(project))
            {
                token.ThrowIfCancellationRequested();
                yield return new ProjectPackage(_logger, _server, project, release, isLatest);
                isLatest = false;
            }
        }

        public IEnumerable<INuGetPackage> FindOctopusProjectPackages(string searchTerm, CancellationToken token)
        {
            var found = false;
            foreach (var project in _octopusCache.GetAllProjects().Where(project => project.Name.WildcardMatch($"*{searchTerm}*")))
            {
                token.ThrowIfCancellationRequested();
                found = true;
                yield return new SearchPackage(_logger, _server, project);
            }
            if (!found)
            {
                var project = _octopusCache.GetProject(searchTerm);
                if (project != null)
                    yield return new SearchPackage(_logger, _server, project);
            }
        }

        public bool IsAuthenticated => _server.IsAuthenticated;
    }
}