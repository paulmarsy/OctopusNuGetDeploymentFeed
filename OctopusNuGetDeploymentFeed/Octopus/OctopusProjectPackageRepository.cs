using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus.Search;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    public abstract class BaseOctopusRepository
    {
        private IOctopusAsyncClient _client;

        private OctopusServerEndpoint _endpoint;

        protected BaseOctopusRepository(string baseUri, string apiKey)
        {
            BaseUri = baseUri;
            ApiKey = apiKey;
        }


        internal OctopusServerEndpoint Endpoint => _endpoint ?? (_endpoint = new OctopusServerEndpoint(BaseUri, ApiKey));
        internal IOctopusAsyncClient Client => _client ?? (_client = OctopusAsyncClient.Create(Endpoint).GetAwaiter().GetResult());
        public string BaseUri { get; }
        public string ApiKey { get; }

        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(BaseUri) || string.IsNullOrWhiteSpace(ApiKey))
                        return false;

                    return Endpoint != null && Client != null;
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    public class OctopusProjectPackageRepository : BaseOctopusRepository, IServerPackageRepository
    {
        private readonly ILogger _logger;
        private readonly IServerPackageRepositoryFactory _serverPackageRepositoryFactory;

        public OctopusProjectPackageRepository(string baseUri, string apiKey, ILogger logger, IServerPackageRepositoryFactory serverPackageRepositoryFactory) : base(baseUri, apiKey)
        {
            _logger = logger;
            _serverPackageRepositoryFactory = serverPackageRepositoryFactory;
        }


        public IDownloadableNuGetPackage GetOctopusReleasePackage(string name, string version, CancellationToken token)
        {
            var project = _serverPackageRepositoryFactory.GetPackageCache(this).GetProject(name);
            if (project == null)
                return null;

            var release = _serverPackageRepositoryFactory.GetPackageCache(this).GetRelease(project, version);
            if (release == null)
                return null;

            var channel = _serverPackageRepositoryFactory.GetPackageCache(this).GetChannel(release.ChannelId);

            return new ReleasePackage(_logger, this, project, release, channel);
        }

        public IEnumerable<INuGetPackage> FindOctopusReleasePackages(string name, CancellationToken token)
        {
            var project = _serverPackageRepositoryFactory.GetPackageCache(this).GetProject(name);
            if (project == null)
                yield break;

            var isLatest = true;
            foreach (var release in _serverPackageRepositoryFactory.GetPackageCache(this).ListReleases(project))
            {
                token.ThrowIfCancellationRequested();
                yield return new ProjectPackage(_logger, this, project, release, isLatest);
                isLatest = false;
            }
        }

        public IEnumerable<INuGetPackage> FindOctopusProjectPackages(string searchTerm, CancellationToken token)
        {
            foreach (var project in _serverPackageRepositoryFactory.GetPackageCache(this).GetAllProjects().Where(project => project.Name.WildcardMatch($"*{searchTerm}*")))
            {
                token.ThrowIfCancellationRequested();
                yield return new SearchPackage(_logger, this, project);
            }
        }

    }
}