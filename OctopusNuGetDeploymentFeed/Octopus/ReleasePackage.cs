using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using Newtonsoft.Json;
using NuGet;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.DataServices;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;

namespace OctopusDeployNuGetFeed.Octopus
{
    /// <summary>
    ///     Package to represent a specific release in detail & provide a deployable nupkg file
    /// </summary>
    public class ReleasePackage : ProjectPackage, IDownloadableNuGetPackage
    {
        private static readonly string DeployPs1;
        private static readonly string DeployConfig;
        private readonly Lazy<byte[]> _nugetPackage;

        static ReleasePackage()
        {
            var assembly = Assembly.GetExecutingAssembly();
            DeployPs1 = GetResource(assembly, "deploy.ps1");
            DeployConfig = GetResource(assembly, "deploy.config");
        }

        public ReleasePackage(ILogger logger, IOctopusServer server, IOctopusCache octopusCache, ProjectResource project, ReleaseResource release, ChannelResource channel) : base(logger, server, project, release, true)
        {
            Cache = octopusCache;
            Channel = channel;
            _nugetPackage = new Lazy<byte[]>(() => Cache.GetNuGetPackage(project, release, CreateNuGetPackage));
        }

        private string SemanticPackageId => PackageIdValidator.IsValidPackageId(Id) ? Id : Project.Id.Replace('-', '.');

        protected IOctopusCache Cache { get; }

        protected ChannelResource Channel { get; }
        public Uri ProjectUrl => new Uri(new Uri(Server.BaseUri), Project.Link("Web"));
        public Uri ReleaseUrl => new Uri(new Uri(Server.BaseUri), Release.Link("Web"));

        protected byte[] NuGetPackage => _nugetPackage.Value;

        public override string Description => $"_Project:_ [{Project.Name}]({ProjectUrl}) <br/>\n" +
                                              $"_Release:_ [{Release.Version}]({ReleaseUrl}) <br/>\n" +
                                              $"_Channel:_ {Channel.Name} <br/>\n" +
                                              $"{GetDescriptionReleaseNotes()}\n" +
                                              $"{GetDescriptionSelectedPackages()}\n";

        public Stream GetStream()
        {
            return new MemoryStream(NuGetPackage);
        }

        public long PackageSize => NuGetPackage.Length;

        private string GetDescriptionReleaseNotes()
        {
            return string.IsNullOrWhiteSpace(Release.ReleaseNotes)
                ? null
                : "_Release Notes_\n\n" +
                  "```\n" +
                  $"{Release.ReleaseNotes.Trim('`')}\n" +
                  "```";
        }

        private string GetDescriptionSelectedPackages()
        {
            if (!Release.SelectedPackages.Any())
                return null;

            var sb = new StringBuilder("_Packages_\n");

            foreach (var selectedPackage in Release.SelectedPackages)
                sb.AppendLine($"- {selectedPackage.StepName} _({selectedPackage.Version})_");

            return sb.ToString();
        }

        private static string GetResource(Assembly assembly, string fileName)
        {
            var resourceName = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith(fileName));
            using (var manifestResourceStream = assembly.GetManifestResourceStream(resourceName))
            using (var streamReader = new StreamReader(manifestResourceStream))
            {
                return streamReader.ReadToEnd();
            }
        }

        private byte[] CreateNuGetPackage()
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    AddJsonDocument(zipArchive, "project.json", Project);
                    AddJsonDocument(zipArchive, "release.json", Release);
                    AddJsonDocument(zipArchive, "channel.json", Channel);
                    AddTextFile(zipArchive, "server.json", JsonConvert.SerializeObject(Server, Formatting.Indented));
                    AddTextFile(zipArchive, "deploy.ps1", DeployPs1);
                    AddTextFile(zipArchive, "deploy.config", DeployConfig);
                    AddTextFile(zipArchive, $"{Id}.nuspec", "<?xml version=\"1.0\"?>" +
                                                            "<package>" +
                                                            "<metadata>" +
                                                            $"<id>{SecurityElement.Escape(SemanticPackageId)}</id>" +
                                                            $"<version>{SecurityElement.Escape(Version)}</version>" +
                                                            $"<description>{SecurityElement.Escape(Description)}</description>" +
                                                            $"<authors>{SecurityElement.Escape(Authors)}</authors>" +
                                                            "</metadata>" +
                                                            "</package>");
                }

                return memoryStream.ToArray();
            }
        }


        private void AddJsonDocument(ZipArchive zipArchive, string fileName, Resource resource)
        {
            using (var stream = zipArchive.CreateEntry(fileName, CompressionLevel.Optimal).Open())
            {
                var json = Cache.GetJson(resource);
                stream.Write(json, 0, json.Length);
            }
        }

        private void AddTextFile(ZipArchive zipArchive, string fileName, string contents)
        {
            using (var stream = zipArchive.CreateEntry(fileName, CompressionLevel.Optimal).Open())
            {
                var buffer = new UTF8Encoding(false).GetBytes(contents);
                stream.Write(buffer, 0, buffer.Length);
            }
        }
    }
}