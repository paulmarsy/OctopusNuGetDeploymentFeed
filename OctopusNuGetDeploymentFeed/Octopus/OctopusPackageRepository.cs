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

        public IDownloadableNuGetPackage GetRelease(string projectName, string version, CancellationToken token)
        {
            var semver = version.ToSemanticVersion();
            if (semver == null)
                return null;

            var project = _octopusCache.GetProject(projectName);
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

        public IEnumerable<INuGetPackage> FindProjectReleases(string projectName, CancellationToken token)
        {
            var project = _octopusCache.GetProject(projectName);
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

        public IEnumerable<INuGetPackage> FindProjects(string searchTerm, CancellationToken token)
        {
            var exactProject = _octopusCache.TryGetProject(searchTerm);
            if (exactProject != null)
                yield return new ProjectPackage(_logger, _server, exactProject, _octopusCache.GetLatestRelease(exactProject), true);
            else
                foreach (var project in _octopusCache.GetAllProjects().Where(project => project.Name.WildcardMatch($"*{searchTerm}*")))
                {
                    token.ThrowIfCancellationRequested();
                    yield return new SearchPackage(_logger, _server, project);
                }
        }

        public bool IsAuthenticated => _server.IsAuthenticated;
    }
}