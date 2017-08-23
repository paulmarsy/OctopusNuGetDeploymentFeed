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

namespace OctopusDeployNuGetFeed.Octopus.Packages
{
    /// <summary>
    ///     Package to represent a specific release in detail & provide a deployable nupkg file
    /// </summary>
    public class ReleasePackage : ProjectPackage, IDownloadableNuGetPackage
    {
        private static readonly string DeployPs1;
        private static readonly string DeployConfig;

        private static readonly UTF8Encoding UTF8Encoding = new UTF8Encoding(false);
        private readonly Lazy<byte[]> _nugetPackage;

        static ReleasePackage()
        {
            var assembly = Assembly.GetExecutingAssembly();
            DeployPs1 = GetResource(assembly, "deploy.ps1");
            DeployConfig = GetResource(assembly, "deploy.config");
        }

        public ReleasePackage(IOctopusServer server, IOctopusCache octopusCache, ProjectResource project, ReleaseResource release, ChannelResource channel) : base(project, release, true)
        {
            Server = server;
            Cache = octopusCache;
            Channel = channel;
            _nugetPackage = new Lazy<byte[]>(() => Cache.GetNuGetPackage(project, release, CreateNuGetPackage));
        }

        private string SemanticPackageId => PackageIdValidator.IsValidPackageId(Id) ? Id : $"{Project.ProjectGroupId}.{Project.Id}".Replace('-', '.');

        protected IOctopusServer Server { get; }
        protected IOctopusCache Cache { get; }

        protected ChannelResource Channel { get; }
        public Uri ProjectUrl => new Uri(new Uri(Server.BaseUri), Project.Link("Web"));
        public Uri ReleaseUrl => new Uri(new Uri(Server.BaseUri), Release.Link("Web"));

        public override string Description => $"_Project:_ [{Project.Name}]({ProjectUrl}) <br/>\n" +
                                              $"_Release:_ [{Release.Version}]({ReleaseUrl}) <br/>\n" +
                                              $"_Channel:_ {Channel.Name} <br/>\n" +
                                              GetDescriptionReleaseNotes() +
                                              GetDescriptionSelectedPackages();

        public Stream GetStream()
        {
            return new MemoryStream(_nugetPackage.Value);
        }

        public long PackageSize => _nugetPackage.Value.Length;

        private string GetDescriptionReleaseNotes()
        {
            return string.IsNullOrWhiteSpace(Release.ReleaseNotes)
                ? null
                : "_Release Notes_\n\n" +
                  "```\n" +
                  $"{Release.ReleaseNotes.Trim('`')}\n" +
                  "```\n";
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
                    AddPackageFile(zipArchive, $"{Id}.nuspec", $@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
    <metadata>
        <id>{SecurityElement.Escape(SemanticPackageId)}</id>
        <version>{SecurityElement.Escape(Version)}</version>
        <description>{SecurityElement.Escape(Summary)}</description>
        <authors>{SecurityElement.Escape(Authors)}</authors>
    </metadata>
</package>");
                    AddPackageFile(zipArchive, "deploy.ps1", DeployPs1);
                    AddPackageFile(zipArchive, "deploy.config", DeployConfig);
                    AddPackageFile(zipArchive, "server.json", JsonConvert.SerializeObject(Server, Formatting.Indented));
                    AddPackageFile(zipArchive, "project.json", Cache.GetJson(Project));
                    AddPackageFile(zipArchive, "channel.json", Cache.GetJson(Channel));
                    AddPackageFile(zipArchive, "release.json", Cache.GetJson(Release));
                }

                return memoryStream.ToArray();
            }
        }

        private static void AddPackageFile(ZipArchive zipArchive, string fileName, string contents)
        {
            using (var stream = zipArchive.CreateEntry(fileName, CompressionLevel.Fastest).Open())
            {
                var buffer = UTF8Encoding.GetBytes(contents);
                stream.Write(buffer, 0, buffer.Length);
            }
        }
    }
}