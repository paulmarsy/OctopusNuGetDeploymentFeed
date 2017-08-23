using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Octopus.Packages;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusPackageRepository : IPackageRepository
    {
        private readonly IOctopusCache _cache;
        private readonly IOctopusServer _server;

        public OctopusPackageRepository(IOctopusServer server, IOctopusCache octopusCache)
        {
            _server = server;
            _cache = octopusCache;
        }

        public IOctopusCache Cache
        {
            get
            {
                Requests++;
                return _cache;
            }
        }

        public int Requests { get; private set; }

        public IDownloadableNuGetPackage GetRelease(string projectName, string version, CancellationToken token)
        {
            var semver = version.ToSemanticVersion();
            if (semver == null)
                return null;

            var project = Cache.GetProject(projectName);
            if (project == null)
                return null;

            var release = Cache.GetRelease(project, semver);
            if (release == null)
                return null;

            var channel = Cache.GetChannel(release.ChannelId);
            if (channel == null)
                return null;

            return new ReleasePackage(_server, _cache, project, release, channel);
        }

        public IEnumerable<INuGetPackage> FindProjectReleases(string projectName, CancellationToken token)
        {
            var project = Cache.GetProject(projectName);
            if (project == null)
                yield break;

            var isLatest = true;
            foreach (var release in Cache.ListReleases(project))
            {
                token.ThrowIfCancellationRequested();
                yield return new ProjectPackage(project, release, isLatest);
                isLatest = false;
            }
        }

        public IEnumerable<INuGetPackage> FindProjects(string searchTerm, CancellationToken token)
        {
            var exactProject = Cache.TryGetProject(searchTerm);
            if (exactProject != null)
                yield return new ProjectPackage(exactProject, Cache.GetLatestRelease(exactProject), true);
            else
                foreach (var project in Cache.GetAllProjects().Where(project => project.Name.WildcardMatch($"*{searchTerm}*")))
                {
                    token.ThrowIfCancellationRequested();
                    yield return new SearchPackage(project);
                }
        }
    }
}