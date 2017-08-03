using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NuGet;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Infrastructure;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    /// <summary>
    ///     Light weight package to represent a project used for searching
    /// </summary>
    public class SearchPackage : INuGetPackage, IPackageMetadata
    {
        public SearchPackage(ILogger logger, OctopusProjectPackageRepository server, ProjectResource project) : this(logger, server, project, new SemanticVersion(1, 0, 0, 0))
        {
        }

        public SearchPackage(ILogger logger, OctopusProjectPackageRepository server, ProjectResource project, SemanticVersion version)
        {
            Logger = logger;
            Server = server;
            Project = project;
            Version = version;
            Logger.Debug($"{GetType().Name}.ctor: {project.Name} {Version}");
        }

        protected ILogger Logger { get; }
        protected OctopusProjectPackageRepository Server { get; }

        protected ProjectResource Project { get; }

        public string Id => Project.Name;

        public SemanticVersion Version { get; }
        public string Title => Project.Name;
        public virtual IEnumerable<string> Authors => new[] {Project.LastModifiedBy ?? "Unknown"};
        public virtual IEnumerable<string> Owners => new[] {Project.LastModifiedBy ?? "Unknown"};
        public Uri IconUrl => new Uri(new Uri(Server.BaseUri), Project.Link("Logo"));
        public Uri LicenseUrl => null;
        public Uri ProjectUrl => new Uri(new Uri(Server.BaseUri), Project.Link("Web"));
        public int DownloadCount => 0;
        public bool RequireLicenseAcceptance => false;
        public bool DevelopmentDependency => false;

        public virtual string Description => $"Octopus Project: {Project.Name} ({Project.Id}) {Project.Description}";
        
        public string Summary => Project.Description;
        public virtual string ReleaseNotes => string.Empty;
        public IEnumerable<PackageDependencySet> DependencySets => Enumerable.Empty<PackageDependencySet>();
        public string Copyright => "Octopus Deploy NuGet Feed by Paul Marston";
        public string Tags => Project.Id;
        public virtual bool IsLatestVersion => true;

        public bool IsAbsoluteLatestVersion => IsLatestVersion;
        public bool Listed => !Project.IsDisabled;
        public Version MinClientVersion => ClientCompatibility.Default.SemVerLevel.Version;
        public string Language => Thread.CurrentThread.CurrentCulture.DisplayName;
        public virtual long PackageSize => 0;
        public virtual string PackageHash => (Id + Version.ToOriginalString()).GetHash(Constants.HashAlgorithm);
        public string PackageHashAlgorithm => Constants.HashAlgorithm;
        public virtual DateTimeOffset LastUpdated => Project.LastModifiedOn.GetValueOrDefault();
        public virtual DateTimeOffset Created => Project.LastModifiedOn.GetValueOrDefault();
        string IPackageName.Id => Project.Id.Replace('-', '.');
        public ICollection<PackageReferenceSet> PackageAssemblyReferences => new List<PackageReferenceSet>();
        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies => Enumerable.Empty<FrameworkAssemblyReference>();
    }
}