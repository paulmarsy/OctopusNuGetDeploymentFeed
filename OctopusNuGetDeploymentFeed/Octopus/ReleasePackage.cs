using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using NuGet;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Infrastructure;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;

namespace OctopusDeployNuGetFeed.Octopus
{
    /// <summary>
    ///     Package to represent a specific release in detail & provide a deployable nupkg file
    /// </summary>
    public class ReleasePackage : ProjectPackage, IDownloadableNuGetPackage
    {
        private static readonly byte[] DeployPs1;
        private static readonly byte[] DeployConfig;
        private byte[] _nugetPackage;

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

        protected ChannelResource Channel { get; }
        public Uri ReleaseUrl => new Uri(new Uri(Server.BaseUri), Release.Link("Web"));


        public override string Description => $"_Project:_ [{Project.Name}]({ProjectUrl}) <br/>\n" +
                                              $"_Release:_ [{Release.Version}]({ReleaseUrl}) <br/>\n" +
                                              $"_Channel:_ {Channel.Name} <br/>\n" +
                                              $"{GetDescriptionReleaseNotes()}\n" +
                                              $"{GetDescriptionSelectedPackages()}\n";


        public override long PackageSize => NuGetPackage.Length;
        public override string PackageHash => GetStream().GetHash(Constants.HashAlgorithm);
        protected byte[] NuGetPackage => _nugetPackage ?? (_nugetPackage = GetNuGetPackage());

        private string GetDescriptionReleaseNotes()
        {
            return string.IsNullOrWhiteSpace(ReleaseNotes)
                ? null
                : $"_Release Notes_\n\n" +
                  $"```\n" +
                  $"{ReleaseNotes.Trim('`')}\n" +
                  $"```";
        }

        private string GetDescriptionSelectedPackages()
        {
            if (!Release.SelectedPackages.Any())
                return null;

            var sb = new StringBuilder("_Packages_\n");

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

        public Stream GetStream()
        {
            return new MemoryStream(NuGetPackage);
        }

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
}