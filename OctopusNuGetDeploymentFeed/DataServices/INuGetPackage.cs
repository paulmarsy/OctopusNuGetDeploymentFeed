using System;
using System.Collections.Generic;
using NuGet;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface INuGetPackage : IPackageMetadata
    {
    
        bool Listed { get; }
        int DownloadCount { get; }
        long PackageSize { get; }
        string PackageHash { get; }
        string PackageHashAlgorithm { get; }
        DateTimeOffset LastUpdated { get; }
        DateTimeOffset Created { get; }
        bool IsLatestVersion { get; }
    }
}