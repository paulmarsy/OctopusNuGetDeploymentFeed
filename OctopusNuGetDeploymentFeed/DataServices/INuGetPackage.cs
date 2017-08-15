using System;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface INuGetPackage
    {
        string Id { get; }
        string Version { get; }
        string Title { get; }
        string Description { get; }
        string Summary { get; }
        string ReleaseNotes { get; }
        bool IsAbsoluteLatestVersion { get; }
        bool IsLatestVersion { get; }
        bool Listed { get; }
        string Authors { get; }
        DateTimeOffset? Published { get; }
    }
}