using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Octopus.Client;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusServer : IOctopusServer, IDisposable
    {
        private readonly IAppInsights _appInsights;
        private readonly ThreadLocal<DependencyTracking> _dependencyTracking = new ThreadLocal<DependencyTracking>();
        private readonly Lazy<OctopusServerEndpoint> _endpoint;
        private readonly ILogger _logger;
        private IHttpOctopusClient _client;
        private IOctopusRepository _repository;
        private RootResource _root;

        public OctopusServer(IAppInsights appInsights, ILogger logger, string baseUri, string apiKey)
        {
            _appInsights = appInsights;
            _logger = logger;
            _endpoint = new Lazy<OctopusServerEndpoint>(() => new OctopusServerEndpoint(baseUri, apiKey));
        }

        private IHttpOctopusClient Client => _client ?? (_client = new OctopusClient(_endpoint.Value));
        public int Requests { get; private set; }

        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    return (_root ?? (_root = GetRepository("Is Authenticated", nameof(OctopusClient)).Client.RefreshRootDocument())) != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void Dispose()
        {
            _dependencyTracking.Dispose();
            _client?.Dispose();
        }

        public string BaseUri => _endpoint.Value.OctopusServer.ToString();
        public string ApiKey => _endpoint.Value.ApiKey;

        public (bool created, string id) RegisterNuGetFeed(string host)
        {
            var existingFeed = GetRepository("Register NuGet Feed", host).Feeds.FindOne(resource => string.Equals(resource.Name, Constants.OctopusNuGetFeedName, StringComparison.OrdinalIgnoreCase) ||
                                                                                                    string.Equals(resource.Username, BaseUri, StringComparison.OrdinalIgnoreCase));
            var feed = new EnhancedNuGetFeedResource(existingFeed)
            {
                Name = Constants.OctopusNuGetFeedName,
                FeedUri = $"http://{host}/nuget",
                Username = BaseUri,
                Password = new SensitiveValue
                {
                    HasValue = true,
                    NewValue = ApiKey
                },
                EnhancedMode = true
            };
            var feedResult = existingFeed == null ? GetRepository("Register NuGet Feed", "create").Feeds.Create(feed) : GetRepository("Register NuGet Feed", "modify").Feeds.Modify(feed);

            return (existingFeed == null, feedResult?.Id);
        }

        internal IHttpOctopusClient GetClient(string operation, string target)
        {
            StartDependencyTracking(operation, target);
            return Client;
        }

        private void StartDependencyTracking(string operation, string target)
        {
            _dependencyTracking.Value = new DependencyTracking(operation, target);
            _logger.Verbose(operation + ": " + target);
            Requests++;
        }

        internal IOctopusRepository GetRepository(string operation, string target)
        {
            StartDependencyTracking(operation, target);
            return _repository ?? (_repository = new OctopusRepository(Client));
        }

        public void ConfigureAppInsightsDependencyTracking()
        {
            _client.SendingOctopusRequest += ClientOnSendingOctopusRequest;
            _client.ReceivedOctopusResponse += ClientOnReceivedOctopusResponse;
        }

        private void ClientOnSendingOctopusRequest(OctopusRequest octopusRequest)
        {
            _dependencyTracking.Value.Start();
        }

        private void ClientOnReceivedOctopusResponse(OctopusResponse octopusResponse)
        {
            _dependencyTracking.Value.Stop();
            _appInsights.TrackDependency($"Octopus Deploy API - {_dependencyTracking.Value.Operation}", _dependencyTracking.Value.Operation, _dependencyTracking.Value.Target, octopusResponse.Request.Uri.PathAndQuery, _dependencyTracking.Value.StartTime, _dependencyTracking.Value.Duration.Elapsed, octopusResponse.StatusCode.ToString(), octopusResponse.StatusCode == HttpStatusCode.OK);
        }

        private class DependencyTracking
        {
            public DependencyTracking(string operation, string target)
            {
                Operation = operation;
                Target = target;
            }

            public DateTimeOffset StartTime { get; private set; }
            public Stopwatch Duration { get; private set; }

            public string Operation { get; }
            public string Target { get; }

            public void Start()
            {
                StartTime = DateTimeOffset.UtcNow;
                Duration = Stopwatch.StartNew();
            }

            public void Stop()
            {
                Duration.Stop();
            }
        }
    }
}