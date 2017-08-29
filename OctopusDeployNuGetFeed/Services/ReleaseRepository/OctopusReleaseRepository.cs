using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Client.Model;
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

        public async Task<IEnumerable<ODataPackage>> GetAllReleasesAsync(string projectName)
        {
            var project = await GetProject(projectName);
            if (!project.exists)
                return Enumerable.Empty<ODataPackage>();

            var releases = await _server.GetAllReleasesAsync(project.value);

            return GetAllReleases(project.value, releases);
        }

        private static IEnumerable<ODataPackage> GetAllReleases(ProjectResource project, IEnumerable<ReleaseResource> releases)
        {
            var isLatest = true;
            foreach (var release in releases)
            {
                yield return new ProjectPackage(project, release, isLatest);
                isLatest = false;
            }
        }

        public async Task<ODataPackage> FindLatestReleaseAsync(string projectName)
        {
            var project = await GetProject(projectName);
            if (!project.exists)
                return null;

            return new ProjectPackage(project.value, await _server.GetLatestReleaseAsync(project.value), true);
        }

        public async Task<ODataPackage> GetReleaseAsync(string projectName, string version) => await GetReleaseImpl(projectName, version);
        private async Task<ReleasePackage> GetReleaseImpl(string projectName, string version)
        {
            var semver = version.ToSemanticVersion();
            if (semver == null)
                return null;

            var project = await GetProject(projectName);
            if (!project.exists)
                return null;

            var release = await _server.GetReleaseAsync(project.value, semver);
            if (release == null)
                return null;

            var channel = await _server.GetChannelAsync(release.ChannelId);
            if (channel == null)
                return null;

            return new ReleasePackage(_connection, _server, project.value, release, channel);
        }

        public async Task<ODataPackageFile> GetPackageAsync(string projectName, string version) => await ODataPackageFile.FromNuGetPackage(await GetReleaseImpl(projectName, version));

        private async Task<(bool exists, ProjectResource value)> GetProject(string projectName)
        {
            var project = await _server.GetProjectAsync(projectName);
            if (project == null)
                return (false, null);

            _server.InitialisePreloader(project);

            return (true, project);
        }
    }
}