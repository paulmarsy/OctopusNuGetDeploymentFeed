using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Logging;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;
using SemanticVersion = NuGet.SemanticVersion;
using System.Reflection;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusServerPackage : IServerPackage, IPackageMetadata
    {
        private static readonly byte[] DeployPs1;
        private readonly bool _detailedView;

        private ProjectResource Project { get; }
        private ReleaseResource Release { get; }
        private readonly ILogger _logger;
        private readonly OctopusProjectPackageRepository _server;
        private ChannelResource _channel;
        private byte[] _nugetPackage;

        static OctopusServerPackage()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith("deploy.ps1"));
            using (var manifestResourceStream = assembly.GetManifestResourceStream(resourceName))
            using (var streamReader = new StreamReader(manifestResourceStream))
            {
                var deployPs1 = streamReader.ReadToEnd();
                DeployPs1 = new UTF8Encoding(false).GetBytes(deployPs1);
            }
        }

        public OctopusServerPackage(ILogger logger, OctopusProjectPackageRepository server, ReleaseResource release, SemanticVersion version, ProjectResource project, bool latestRelease, bool detailedView)
        {
            Version = version;
            _logger = logger;
            _server = server;
            Release = release;
            Project = project;
            _detailedView = detailedView;
            IsLatestVersion = latestRelease;
            _logger.Debug($"OctopusServerPackage.ctor: {project.Name} ({project.Id}) {release.Version}");
        }

        private byte[] NuGetPackage => _nugetPackage ?? (_nugetPackage = GetNuGetPackage());
        public Uri ReleaseUrl => new Uri(new Uri(_server.BaseUri), Release.Link("Web"));
        string IPackageName.Id => Project.Id + '.' + Release.Id;
        public ICollection<PackageReferenceSet> PackageAssemblyReferences => new List<PackageReferenceSet>();
        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies => Enumerable.Empty<FrameworkAssemblyReference>();

        public string Id => Project.Name;

        public SemanticVersion Version { get; }
        public string Title => Project.Name;
        public IEnumerable<string> Authors => new[] {Release.LastModifiedBy ?? "Unknown"};
        public IEnumerable<string> Owners => new[] {Project.LastModifiedBy ?? "Unknown"};
        public Uri IconUrl => new Uri(new Uri(_server.BaseUri), Project.Link("Logo"));
        public Uri LicenseUrl => null;
        public Uri ProjectUrl => new Uri(new Uri(_server.BaseUri), Project.Link("Web"));
        public int DownloadCount => 0;
        public bool RequireLicenseAcceptance => false;
        public bool DevelopmentDependency => false;
        private ChannelResource Channel => _channel ?? (_channel = _server.Client.Repository.Channels.Get(Release.ChannelId).GetAwaiter().GetResult());
        public string Description
        {
            get
            {
                if (_detailedView)
                    return $"Project: [{Project.Name}]({ProjectUrl}) <br/>\nRelease: [{Release.Version}]({ReleaseUrl}) <br/>\nChannel: {Channel.Name} <br/>\n{(string.IsNullOrWhiteSpace(ReleaseNotes) ? null : $"Release Notes:\n\n{ReleaseNotes}")}";
                return $"Octopus Project: {Project.Name} ({Project.Id}) {(string.IsNullOrWhiteSpace(Project.Description) ? null :Project.Description)}";
            }
        }

        public string Summary => Project.Description;
        public string ReleaseNotes => Release.ReleaseNotes;
        public IEnumerable<PackageDependencySet> DependencySets => Enumerable.Empty<PackageDependencySet>();
        public string Copyright { get; }
        public string Tags { get; }
        public bool IsLatestVersion { get; }

        public bool IsAbsoluteLatestVersion => IsLatestVersion;
        public bool Listed => !Project.IsDisabled;
        public Version MinClientVersion { get; }
        public string Language { get; }
        public long PackageSize => _detailedView ? NuGetPackage.Length : 0;
        public string PackageHash => _detailedView ? this.GetHash(Constants.HashAlgorithm) : null;
        public string PackageHashAlgorithm => Constants.HashAlgorithm;
        public DateTimeOffset LastUpdated => Release.LastModifiedOn.GetValueOrDefault();
        public DateTimeOffset Created => Release.Assembled;

        public Stream GetStream()
        {
            return new MemoryStream(NuGetPackage);
        }

        public byte[] GetNuGetPackage()
        {
            _logger.Info($"OctopusServerPackage.GetNuGetPackage: {Project.Name} {Release.Version}");
            using (var memoryStream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var manifest = Manifest.Create(this);
                    manifest.Files = new List<ManifestFile>();

                    AddFile(zipArchive, manifest, "project.json",  stream => GetResourceJson(Project, stream));
                    AddFile(zipArchive, manifest, "release.json",  stream => GetResourceJson(Release, stream));
                    AddFile(zipArchive, manifest, "channel.json",  stream => GetResourceJson(Channel, stream));
                    AddFile(zipArchive, manifest, "server.json", stream => _server.SerializeInto(stream));
                    AddFile(zipArchive, manifest, "deploy.ps1", stream =>stream.Write(DeployPs1,0,DeployPs1.Length));
                    
                    using (var stream = zipArchive.CreateEntry($"{Id}.nuspec", CompressionLevel.Fastest).Open())
                    {
                        manifest.Save(stream);
                    }
                }
                return memoryStream.ToArray();
            }
        }

        private void AddFile(ZipArchive zipArchive, Manifest manifest, string fileName, Action<Stream> stream)
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

        private  void GetResourceJson(Resource resource, Stream destStream)
        {
            using (var sourceStream = _server.Client.GetContent(resource.Link("Self")).GetAwaiter().GetResult())
            {
                sourceStream.CopyTo(destStream);
            }
        }
    }
}