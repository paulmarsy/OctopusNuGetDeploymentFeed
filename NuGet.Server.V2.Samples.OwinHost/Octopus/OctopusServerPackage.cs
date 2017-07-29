using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.Server.Core.Infrastructure;
using NuGet.Server.Core.Logging;
using Octopus.Client.Model;

namespace NuGet.Server.V2.Samples.OwinHost.Octopus
{
    public class OctopusServerPackage : IServerPackage, IPackageMetadata
    {
        private readonly IServerPackageRepository _server;
        private readonly ReleaseResource _release;
        private readonly ProjectResource _project;
        private readonly bool _latestRelease;

        public OctopusServerPackage(Core.Logging.ILogger logger, IServerPackageRepository server, ReleaseResource release, SemanticVersion version, ProjectResource project, bool latestRelease)
        {
            Version = version;
#if DEBUG
            logger.Log(LogLevel.Verbose,$"OctopusServerPackage.ctor: {project.Name} ({project.Id}) {release.Version}");
#endif
            _server = server;
            _release = release;
            _project = project;
            _latestRelease = latestRelease;
        }

        public string Id => _project.Name;
         string IPackageName.Id => _project.Id + '.' +_release.Id;

        public SemanticVersion Version { get; }
        public string Title => _project.Name;
        public IEnumerable<string> Authors => new[] {_release.LastModifiedBy ?? "Unknown"};
        public IEnumerable<string> Owners => new[] {_project.LastModifiedBy ?? "Unknown"};
        public Uri IconUrl => new Uri(new Uri(_server.BaseUri), _project.Link("Logo"));
        public Uri LicenseUrl => null;
        public Uri ProjectUrl => new Uri(new Uri(_server.BaseUri), _project.Link("Web"));
        public int DownloadCount => 0;
        public bool RequireLicenseAcceptance => false;
        public bool DevelopmentDependency => false;
        public string Description => string.IsNullOrWhiteSpace(_project.Description) ? _project.Name : _project.Description;
        public string Summary => _project.Description;
        public string ReleaseNotes => _release.ReleaseNotes;
        public ICollection<PackageReferenceSet> PackageAssemblyReferences => new List<PackageReferenceSet>();
        public IEnumerable<PackageDependencySet> DependencySets => Enumerable.Empty<PackageDependencySet>();
        public string Copyright { get; }
        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies => Enumerable.Empty<FrameworkAssemblyReference>();
        public string Tags { get; }
        public bool IsLatestVersion => _latestRelease;
        public bool IsAbsoluteLatestVersion => _latestRelease;
        public bool Listed => !_project.IsDisabled;
        public Version MinClientVersion { get; }
        public string Language { get; }
        public long PackageSize => NuGetPackage.Length;
        public string PackageHash => Server.Core.DataServices.PackageExtensions.GetHash(this, Core.Constants.HashAlgorithm);
        public string PackageHashAlgorithm => Core.Constants.HashAlgorithm;
        public DateTimeOffset LastUpdated => _release.LastModifiedOn.GetValueOrDefault();
        public DateTimeOffset Created => _release.Assembled;
        public Stream GetStream() => new MemoryStream(NuGetPackage);
        private byte[] _nugetPackage;
        private byte[] NuGetPackage => _nugetPackage ?? (_nugetPackage = GetNuGetPackage());

        public byte[] GetNuGetPackage()
        {            
            using (var memoryStream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var manifest = Manifest.Create(this);
                    manifest.Files = new List<ManifestFile>();

                    using (var stream = zipArchive.CreateEntry("project.json", CompressionLevel.Fastest).Open())
                    {
                        _project.SerializeInto(stream);
                        manifest.Files.Add(new ManifestFile
                        {
                            Source = "project.json",
                            Target = "project.json"
                        });
                    }

                    using (var stream = zipArchive.CreateEntry("release.json", CompressionLevel.Fastest).Open())
                    {
                        _release.SerializeInto(stream);
                        manifest.Files.Add(new ManifestFile
                        {
                            Source = "release.json",
                            Target = "release.json"
                        });
                    }
                    using (var stream = zipArchive.CreateEntry("server.json", CompressionLevel.Fastest).Open())
                    {
                        _server.SerializeInto(stream);
                        manifest.Files.Add(new ManifestFile
                        {
                            Source = "server.json",
                            Target = "server.json"
                        });
                    }
                    using (var stream = zipArchive.CreateEntry($"{Id}.nuspec", CompressionLevel.Fastest).Open())
                    {
                        manifest.Save(stream);
                    }

                }
                return memoryStream.ToArray();
            }
        }
    }
}