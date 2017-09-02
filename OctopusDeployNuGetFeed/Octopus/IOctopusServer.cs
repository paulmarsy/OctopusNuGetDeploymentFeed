using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Client.Model;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IOctopusServer
    {
        int CachedItems { get; }
        long CacheSize { get; }
        int PreloadCount { get; }
        IEnumerable<ProjectResource> GetAllProjects();
        Task<ChannelResource> GetChannelAsync(string channelId);
        Task<string> GetJsonAsync(Resource resource);
        Task<ReleaseResource> GetReleaseAsync(ProjectResource project, SemanticVersion semver);
        Task<byte[]> GetNuGetPackageAsync(ProjectResource project, ReleaseResource release, Func<Task<byte[]>> nugetPackageFactory);
        Task<ReleaseResource> GetLatestReleaseAsync(ProjectResource project);
        void InitialisePreloader();
        void InitialisePreloader(ProjectResource project);
        Task<ProjectResource> GetProjectAsync(string name);
        Task<IList<ReleaseResource>> GetAllReleasesAsync(ProjectResource project);
        bool ProjectExists(string projectName);
    }
}