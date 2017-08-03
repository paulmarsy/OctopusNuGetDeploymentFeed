using System;
using System.Collections.Generic;
using NuGet;
using Octopus.Client.Model;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    /// <summary>
    ///     Package to represent the releases in a project
    /// </summary>
    public class ProjectPackage : SearchPackage, IPackageMetadata
    {
        public ProjectPackage(ILogger logger, OctopusProjectPackageRepository server, ProjectResource project, ReleaseResource release, bool isLatest) : base(logger, server, project, SemanticVersion.Parse(release.Version))
        {
            Release = release;
            IsLatestVersion = isLatest;
        }

        protected ReleaseResource Release { get; }
        public override bool IsLatestVersion { get; }
        public override DateTimeOffset LastUpdated => Release.LastModifiedOn.GetValueOrDefault();
        public override DateTimeOffset Created => Release.Assembled;
        string IPackageName.Id => Project.Id + '.' + Release.Id;
        public override IEnumerable<string> Authors => new[] {Release.LastModifiedBy ?? "Unknown"};

        public override string ReleaseNotes => Release.ReleaseNotes;
    }
}