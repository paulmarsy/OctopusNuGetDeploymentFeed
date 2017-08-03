using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using OctopusDeployNuGetFeed.Infrastructure;

namespace OctopusDeployNuGetFeed.DataServices
{
    public static class PackageExtensions
    {
     

        public static ODataPackage AsODataPackage(this INuGetPackage package, ClientCompatibility compatibility)
        {
            return new ODataPackage
            {
                Id = package.Id,
                Version = package.Version.ToOriginalString(),
                NormalizedVersion = package.Version.ToNormalizedString(),
                IsPrerelease = !string.IsNullOrWhiteSpace(package.Version.SpecialVersion),
                Title = package.Title,
                Authors = string.Join(",", package.Authors),
                Owners = string.Join(",", package.Owners),
                IconUrl = package.IconUrl == null ? null : package.IconUrl.GetComponents(UriComponents.HttpRequestUrl, UriFormat.Unescaped),
                LicenseUrl = package.LicenseUrl == null ? null : package.LicenseUrl.GetComponents(UriComponents.HttpRequestUrl, UriFormat.Unescaped),
                ProjectUrl = package.ProjectUrl == null ? null : package.ProjectUrl.GetComponents(UriComponents.HttpRequestUrl, UriFormat.Unescaped),
                DownloadCount = package.DownloadCount,
                RequireLicenseAcceptance = package.RequireLicenseAcceptance,
                DevelopmentDependency = package.DevelopmentDependency,
                Description = package.Description,
                Summary = package.Summary,
                ReleaseNotes = package.ReleaseNotes,
                Published = package.Created.UtcDateTime,
                LastUpdated = package.LastUpdated.UtcDateTime,
                Dependencies = string.Join("|", package.DependencySets.SelectMany(ConvertDependencySetToStrings)),
                PackageHash = package.PackageHash,
                PackageHashAlgorithm = package.PackageHashAlgorithm,
                PackageSize = package.PackageSize,
                Copyright = package.Copyright,
                Tags = package.Tags,
                IsAbsoluteLatestVersion = package.IsAbsoluteLatestVersion,
                IsLatestVersion = package.IsLatestVersion,
                Listed = package.Listed,
                VersionDownloadCount = package.DownloadCount,
                MinClientVersion = package.MinClientVersion == null ? null : package.MinClientVersion.ToString(),
                Language = package.Language
            };
        }

        private static IEnumerable<string> ConvertDependencySetToStrings(PackageDependencySet dependencySet)
        {
            if (dependencySet.Dependencies.Count == 0)
            {
                if (dependencySet.TargetFramework != null)
                    return new[] {$"::{VersionUtility.GetShortFrameworkName(dependencySet.TargetFramework)}"};
            }
            else
            {
                return dependencySet.Dependencies.Select(dependency => ConvertDependency(dependency, dependencySet.TargetFramework));
            }

            return new string[0];
        }

        private static string ConvertDependency(PackageDependency packageDependency, FrameworkName targetFramework)
        {
            if (targetFramework == null)
                if (packageDependency.VersionSpec == null)
                    return packageDependency.Id;
                else
                    return $"{packageDependency.Id}:{packageDependency.VersionSpec}";
            return $"{packageDependency.Id}:{packageDependency.VersionSpec}:{VersionUtility.GetShortFrameworkName(targetFramework)}";
        }
    }
}