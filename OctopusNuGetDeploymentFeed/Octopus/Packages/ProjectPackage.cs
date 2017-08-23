using System;
using Octopus.Client.Model;

namespace OctopusDeployNuGetFeed.Octopus.Packages
{
    /// <summary>
    ///     Package to represent the releases in a project
    /// </summary>
    public class ProjectPackage : SearchPackage
    {
        public ProjectPackage(ProjectResource project, ReleaseResource release, bool isLatest) : base(project)
        {
            Release = release;
            IsLatestVersion = isLatest;
            IsAbsoluteLatestVersion = isLatest;
        }

        public override string Version => Release.Version;
        protected ReleaseResource Release { get; }
        public override bool IsLatestVersion { get; }
        public override bool IsAbsoluteLatestVersion { get; }
        public override DateTimeOffset? Published => Release.Assembled;
        public override string Authors => Release.LastModifiedBy ?? "Unknown";
        public override string ReleaseNotes => Release.ReleaseNotes;
    }
}