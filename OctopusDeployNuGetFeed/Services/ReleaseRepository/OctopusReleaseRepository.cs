using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Services.ReleaseRepository
{
    public class OctopusReleaseRepository : IReleaseRepository
    {
        private readonly IOctopusConnection _connection;
        private readonly IOctopusServer _server;

        public OctopusReleaseRepository(IOctopusConnection connection, IOctopusServer server)
        {
            _connection = connection;
            _server = server;
        }

        public Task<IEnumerable<ODataPackage>> GetAllReleasesAsync(string projectName)
        {
            return Task.FromResult(GetAllReleases(projectName).Select(ODataPackage.FromNuGetPackage));
        }

        public Task<ODataPackage> FindLatestReleaseAsync(string projectName)
        {
            return Task.FromResult(ODataPackage.FromNuGetPackage(FindLatestRelease(projectName)));
        }

        public Task<ODataPackage> GetReleaseAsync(string projectName, string version)
        {
            return Task.FromResult(ODataPackage.FromNuGetPackage(GetRelease(projectName, version)));
        }

        public Task<ODataPackageFile> GetPackageAsync(string projectName, string version)
        {
            return Task.FromResult(ODataPackageFile.FromNuGetPackage(GetRelease(projectName, version)));
        }

        private IDownloadableNuGetPackage GetRelease(string projectName, string version)
        {
            var semver = version.ToSemanticVersion();
            if (semver == null)
                return null;

            var project = _server.GetProject(projectName);
            if (project == null)
                return null;

            _server.InitialisePreloader(project);

            var release = _server.GetRelease(project, semver);
            if (release == null)
                return null;

            var channel = _server.GetChannel(release.ChannelId);
            if (channel == null)
                return null;

            return new ReleasePackage(_connection, _server, project, release, channel);
        }

        private IEnumerable<INuGetPackage> GetAllReleases(string projectName)
        {
            var project = _server.GetProject(projectName);
            if (project == null)
                yield break;

            _server.InitialisePreloader(project);

            var isLatest = true;
            foreach (var release in _server.ListReleases(project))
            {
                yield return new ProjectPackage(project, release, isLatest);
                isLatest = false;
            }
        }

        private INuGetPackage FindLatestRelease(string projectName)
        {
            var project = _server.TryGetProject(projectName);
            if (project == null)
                return null;

            _server.InitialisePreloader(project);

            return new ProjectPackage(project, _server.GetLatestRelease(project), true);
        }
    }
}