using System;
using System.Collections.Generic;
using NuGet;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface INuGetPackage
    {
        string Id { get; }
        SemanticVersion Version { get; }
        string Title { get; }
        IEnumerable<string> Authors { get; }
        IEnumerable<string> Owners { get; }
        Uri IconUrl { get; }
        Uri LicenseUrl { get; }
        Uri ProjectUrl { get; }
        int DownloadCount { get; }
        bool RequireLicenseAcceptance { get; }
        bool DevelopmentDependency { get; }
        string Description { get; }
        string Summary { get; }
        string ReleaseNotes { get; }
        IEnumerable<PackageDependencySet> DependencySets { get; }
        string Copyright { get; }
        string Tags { get; }
        bool Listed { get; }
        Version MinClientVersion { get; }
        string Language { get; }
        long PackageSize { get; }
        string PackageHash { get; }
        string PackageHashAlgorithm { get; }
        DateTimeOffset LastUpdated { get; }
        DateTimeOffset Created { get; }
        bool IsLatestVersion { get; }
        bool IsAbsoluteLatestVersion { get; }
    }
}