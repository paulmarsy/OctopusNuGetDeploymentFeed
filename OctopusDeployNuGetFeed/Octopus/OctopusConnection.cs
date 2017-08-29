using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusConnection : IOctopusConnection, IDisposable
    {
        private readonly IAppInsights _appInsights;
        private readonly AsyncLocal<DependencyTracking> _dependencyTracking = new AsyncLocal<DependencyTracking>();
        private readonly Lazy<OctopusServerEndpoint> _endpoint;
        private readonly ILogger _logger;
        private IOctopusAsyncClient _client;
        private IOctopusAsyncRepository _repository;
        private bool? _isAuthenticated;

        public OctopusConnection(IAppInsights appInsights, ILogger logger, string baseUri, string apiKey)
        {
            _appInsights = appInsights;
            _logger = logger;
            _endpoint = new Lazy<OctopusServerEndpoint>(() => new OctopusServerEndpoint(baseUri, apiKey));
        }

        public async Task<bool> IsAuthenticated()
        {
            if (_isAuthenticated.HasValue && _isAuthenticated.Value)
                return true;
            try
            {
                _isAuthenticated = await GetRepositoryAsync("Is Authenticated", nameof(OctopusClient)) != null;
                return _isAuthenticated.Value;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

        public string BaseUri => _endpoint.Value.OctopusServer.ToString();
        public string ApiKey => _endpoint.Value.ApiKey;

        public async Task<(bool created, string id)> RegisterNuGetFeed(string host)
        {
            var repo = GetRepository("Register NuGet Feed", host);
            var existingFeed = await repo.Feeds.FindOne(resource => string.Equals(resource.Name, Constants.OctopusNuGetFeedName, StringComparison.OrdinalIgnoreCase) ||
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
            if (existingFeed == null)
            {
                var newFeed = await repo.Feeds.Create(feed);
                return (true, newFeed.Id);
            }
            else
            {
                var updatedFeed = await repo.Feeds.Modify(feed);
                return (false, updatedFeed.Id);
            }
        }

        public IOctopusAsyncClient GetClient(string operation, string target)
        {
            StartDependencyTracking(operation, target);
            return _client ?? (_client = GetClientAsync(operation, target).GetAwaiter().GetResult());
        }
        private async Task<IOctopusAsyncClient> GetClientAsync(string operation, string target)
        {
            StartDependencyTracking(operation, target);
            return _client ?? (_client = await OctopusAsyncClient.Create(_endpoint.Value));
        }

        private void StartDependencyTracking(string operation, string target)
        {
            _dependencyTracking.Value = new DependencyTracking(operation, target);
            _logger.Verbose(operation + ": " + target);
        }

        public IOctopusAsyncRepository GetRepository(string operation, string target)
        {
            StartDependencyTracking(operation, target);
            return _repository ?? (_repository = new OctopusAsyncRepository(GetClient(operation, target)));
        }
        private async Task<IOctopusAsyncRepository> GetRepositoryAsync(string operation, string target)
        {
            StartDependencyTracking(operation, target);
            return _repository ?? (_repository = new OctopusAsyncRepository(await GetClientAsync(operation, target)));
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