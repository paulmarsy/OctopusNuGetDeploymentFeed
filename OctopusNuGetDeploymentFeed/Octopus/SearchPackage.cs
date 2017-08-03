using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Infrastructure;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus.Search
{
    /// <summary>
    /// Package to represent a specific release in detail & provide a deployable nupkg file
    /// </summary>
    public class ReleasePackage : ProjectPackage, IDownloadableNuGetPackage
    {
    protected ChannelResource Channel { get; }
        private static readonly byte[] DeployPs1;
        private static readonly byte[] DeployConfig;

        static ReleasePackage()
        {
            var assembly = Assembly.GetExecutingAssembly();
            DeployPs1 = GetResourceBytes(assembly, "deploy.ps1");
            DeployConfig = GetResourceBytes(assembly, "deploy.config");
        }

        public ReleasePackage(ILogger logger, OctopusProjectPackageRepository server, ProjectResource project, ReleaseResource release, ChannelResource channel) : base(logger, server, project, release, true)
        {
            Channel = channel;
        }
        public Uri ReleaseUrl => new Uri(new Uri(Server.BaseUri), Release.Link("Web"));


        public override string Description=> $"_Project:_ [{Project.Name}]({ProjectUrl}) <br/>\n" +
                           $"_Release:_ [{Release.Version}]({ReleaseUrl}) <br/>\n" +
                           $"_Channel:_ {Channel.Name} <br/>\n" +
                           $"{GetDescriptionReleaseNotes()}\n" +
                           $"{GetDescriptionSelectedPackages()}\n";
         
        private string GetDescriptionReleaseNotes() => string.IsNullOrWhiteSpace(ReleaseNotes) ? null : $"_Release Notes_\n" +
                                                                                                        $"```\n" +
                                                                                                        $"{ReleaseNotes.Trim('`')}\n" +
                                                                                                        $"```";

        private string GetDescriptionSelectedPackages()
        {
            if (!Release.SelectedPackages.Any())
                return null;

            var sb = new StringBuilder("_Packages_");

            foreach (var selectedPackage in Release.SelectedPackages)
                sb.AppendLine($"- {selectedPackage.StepName} _{selectedPackage.Version}_");

            return sb.ToString();
        }
        private static byte[] GetResourceBytes(Assembly assembly, string fileName)
        {
            var resourceName = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith(fileName));
            using (var manifestResourceStream = assembly.GetManifestResourceStream(resourceName))
            using (var streamReader = new StreamReader(manifestResourceStream))
            {
                var resourceText = streamReader.ReadToEnd();
                return new UTF8Encoding(false).GetBytes(resourceText);
            }
        }
        private byte[] _nugetPackage;


        public override long PackageSize =>  NuGetPackage.Length;
        public override string PackageHash => GetStream().GetHash(Constants.HashAlgorithm);
        protected byte[] NuGetPackage => _nugetPackage ?? (_nugetPackage = GetNuGetPackage());
        private byte[] GetNuGetPackage()
        {
            Logger.Info($"ReleasePackage.GetNuGetPackage: {Project.Name} {Release.Version}");
            using (var memoryStream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var manifest = Manifest.Create(this);
                    manifest.Files = new List<ManifestFile>();

                    AddFile(zipArchive, manifest, "project.json", stream => GetResourceJson(Project, stream));
                    AddFile(zipArchive, manifest, "release.json", stream => GetResourceJson(Release, stream));
                    AddFile(zipArchive, manifest, "channel.json", stream => GetResourceJson(Channel, stream));
                    AddFile(zipArchive, manifest, "server.json", stream => Server.SerializeInto(stream));
                    AddFile(zipArchive, manifest, "deploy.ps1", stream => stream.Write(DeployPs1, 0, DeployPs1.Length));
                    AddFile(zipArchive, manifest, "deploy.config", stream => stream.Write(DeployConfig, 0, DeployConfig.Length));

                    using (var stream = zipArchive.CreateEntry($"{Id}.nuspec", CompressionLevel.Fastest).Open())
                    {
                        manifest.Save(stream);
                    }
                }
                return memoryStream.ToArray();
            }
        }
        public Stream GetStream()=> new MemoryStream(NuGetPackage);

        private static void AddFile(ZipArchive zipArchive, Manifest manifest, string fileName, Action<Stream> stream)
        {
            using (var entryStream = zipArchive.CreateEntry(fileName, CompressionLevel.Fastest).Open())
            {
                stream(entryStream);
                manifest.Files.Add(new ManifestFile
                {
                    Source = fileName,
                    Target = fileName
                });
            }
        }

        private void GetResourceJson(Resource resource, Stream destStream)
        {
            using (var sourceStream = Server.Client.GetContent(resource.Link("Self")).GetAwaiter().GetResult())
            {
                sourceStream.CopyTo(destStream);
            }
        }
    }

    /// <summary>
    /// Package to represent the releases in a project
    /// </summary>
    public class ProjectPackage : SearchPackage, IPackageMetadata
    {
        protected ReleaseResource Release { get; }

        public ProjectPackage(ILogger logger, OctopusProjectPackageRepository server, ProjectResource project, ReleaseResource release, bool isLatest) : base(logger, server, project)
        {
            Release = release;
            IsLatestVersion = isLatest;
        }
        string IPackageName.Id => Project.Id + '.' + Release.Id;
        public override IEnumerable<string> Authors => new[] { Release.LastModifiedBy ?? "Unknown" };

        public override string ReleaseNotes => Release.ReleaseNotes;
        public override SemanticVersion Version => SemanticVersion.Parse(Release.Version);
        public override bool IsLatestVersion { get; }
        public override DateTimeOffset LastUpdated => Release.LastModifiedOn.GetValueOrDefault();
        public override DateTimeOffset Created => Release.Assembled;
    }

    /// <summary>
    /// Light weight package to represent a project used for searching
    /// </summary>
public class SearchPackage : INuGetPackage, IPackageMetadata
    {
        protected ILogger Logger { get; }
        protected OctopusProjectPackageRepository Server { get; }

        public SearchPackage(ILogger logger, OctopusProjectPackageRepository server, ProjectResource project)
        {
            Logger = logger;
            Server = server;
            Project = project;
            Logger.Debug($"{GetType().Name}.ctor: {project.Name} {Version}");
        }

        protected ProjectResource Project { get; }
        string IPackageName.Id => Project.Id.Replace('-','.');
        public ICollection<PackageReferenceSet> PackageAssemblyReferences => new List<PackageReferenceSet>();
        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies => Enumerable.Empty<FrameworkAssemblyReference>();

        public string Id => Project.Name;

        public virtual SemanticVersion Version => new SemanticVersion(1, 0, 0, 0);
    public string Title => Project.Name;
        public virtual IEnumerable<string> Authors => new[] { Project.LastModifiedBy ?? "Unknown" };
        public virtual IEnumerable<string> Owners => new[] { Project.LastModifiedBy ?? "Unknown" };
        public Uri IconUrl => new Uri(new Uri(Server.BaseUri), Project.Link("Logo"));
        public Uri LicenseUrl => null;
        public Uri ProjectUrl => new Uri(new Uri(Server.BaseUri), Project.Link("Web"));
        public int DownloadCount => 0;
        public bool RequireLicenseAcceptance => false;
        public bool DevelopmentDependency => false;

        public virtual string Description=> $"Octopus Project: {Project.Name} ({Project.Id}) {Project.Description}";


        public string Summary => Project.Description;
        public virtual string ReleaseNotes => null;
        public IEnumerable<PackageDependencySet> DependencySets => Enumerable.Empty<PackageDependencySet>();
        public string Copyright => "Octopus Deploy NuGet Feed by Paul Marston";
        public string Tags => Project.Id;
        public virtual bool IsLatestVersion => true;

        public bool IsAbsoluteLatestVersion => IsLatestVersion;
        public bool Listed => !Project.IsDisabled;
        public Version MinClientVersion { get; }
        public string Language => Thread.CurrentThread.CurrentCulture.DisplayName;
        public virtual long PackageSize => 0;
        public virtual string PackageHash => (Id + Version.ToOriginalString()).GetHash(Constants.HashAlgorithm);
        public string PackageHashAlgorithm => Constants.HashAlgorithm;
        public virtual DateTimeOffset LastUpdated => Project.LastModifiedOn.GetValueOrDefault();
        public virtual DateTimeOffset Created => Project.LastModifiedOn.GetValueOrDefault();
    }
}
