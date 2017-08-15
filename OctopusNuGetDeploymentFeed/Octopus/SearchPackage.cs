using System;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus
{
    /// <summary>
    ///     Light weight package to represent a project used for searching
    /// </summary>
    public class SearchPackage : INuGetPackage
    {
        public SearchPackage(ILogger logger, IOctopusServer server, ProjectResource project) : this(logger, server, project, "0.0.0")
        {
        }

        public SearchPackage(ILogger logger, IOctopusServer server, ProjectResource project, string version)
        {
            Logger = logger;
            Server = server;
            Project = project;
            Version = version;
        }

        protected ILogger Logger { get; }
        protected IOctopusServer Server { get; }
        protected ProjectResource Project { get; }
        public string Id => Project.Name;
        public virtual bool IsAbsoluteLatestVersion => true;
        public virtual string Authors => Project.LastModifiedBy ?? "Unknown";
        public virtual DateTimeOffset? Published => Project.LastModifiedOn;
        public virtual string Version { get; }
        public string Title => Project.Name;
        public virtual string Description => Summary;
        public string Summary => $"Octopus Project: {Project.Name}. {Project.Description}";
        public virtual string ReleaseNotes => string.Empty;
        public virtual bool IsLatestVersion => true;
        public bool Listed => !Project.IsDisabled;
    }
}