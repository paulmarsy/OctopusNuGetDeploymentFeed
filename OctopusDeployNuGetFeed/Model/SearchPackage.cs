using System;
using Octopus.Client.Model;

namespace OctopusDeployNuGetFeed.Model
{
    /// <summary>
    ///     Light weight package to represent a project used for searching
    /// </summary>
    public class SearchPackage : INuGetPackage
    {
        public SearchPackage(ProjectResource project)
        {
            Project = project;
        }

        protected ProjectResource Project { get; }
        public string Id => Project.Name;
        public virtual bool IsAbsoluteLatestVersion => true;
        public virtual string Authors => Project.LastModifiedBy ?? "Unknown";
        public virtual DateTimeOffset? Published => Project.LastModifiedOn;
        public virtual string Version => "0.0.0";
        public string Title => Project.Name;
        public virtual string Description => Summary;
        public string Summary => $"Octopus Project: {Project.Name}. {Project.Description}";
        public virtual string ReleaseNotes => string.Empty;
        public virtual bool IsLatestVersion => true;
        public bool Listed => !Project.IsDisabled;
    }
}