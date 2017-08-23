using System;
using System.Collections.Generic;
using Octopus.Client.Model;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IOctopusCache
    {
        int Count { get; }
        long ApproximateSize { get; }
        int PreloadCount { get; }
        ProjectResource GetProject(string name);
        IEnumerable<ProjectResource> GetAllProjects();
        ChannelResource GetChannel(string channelId);
        IEnumerable<ReleaseResource> ListReleases(ProjectResource project);
        string GetJson(Resource resource);
        ReleaseResource GetRelease(ProjectResource project, SemanticVersion semver);
        byte[] GetNuGetPackage(ProjectResource project, ReleaseResource release, Func<byte[]> nugetPackageFactory);
        ProjectResource TryGetProject(string name);
        ReleaseResource GetLatestRelease(ProjectResource project);
    }
}