using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Octopus.Client;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusServer : IOctopusServer
    {
        private readonly IAppInsights _appInsights;
        private readonly ThreadLocal<string> _dependencyContext = new ThreadLocal<string>();
        private readonly ConcurrentDictionary<OctopusRequest, (DateTimeOffset startTime, Stopwatch duration)> _dependencyTracking = new ConcurrentDictionary<OctopusRequest, (DateTimeOffset startTime, Stopwatch duration)>();
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
        public string BaseUri => _endpoint.Value.OctopusServer.ToString();
        public string ApiKey => _endpoint.Value.ApiKey;

        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    return (_root ?? (_root = GetRepository("OctopusServer.IsAuthenticated").Client.RefreshRootDocument())) != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal IHttpOctopusClient GetClient(string context)
        {
            _logger.Info(context);
            _dependencyContext.Value = context;
            return Client;
        }

        internal IOctopusRepository GetRepository(string context)
        {
            _logger.Info(context);
            _dependencyContext.Value = context;
            return _repository ?? (_repository = new OctopusRepository(Client));
        }

        public void ConfigureAppInsightsDependencyTracking()
        {
            _client.SendingOctopusRequest += ClientOnSendingOctopusRequest;
            _client.ReceivedOctopusResponse += ClientOnReceivedOctopusResponse;
        }

        private void ClientOnSendingOctopusRequest(OctopusRequest octopusRequest)
        {
            _dependencyTracking[octopusRequest] = (DateTimeOffset.UtcNow, Stopwatch.StartNew());
        }

        private void ClientOnReceivedOctopusResponse(OctopusResponse octopusResponse)
        {
            if (_dependencyTracking.TryRemove(octopusResponse.Request, out (DateTimeOffset startTime, Stopwatch duration) tracking))
            {
                tracking.duration.Stop();
                _appInsights.TrackDependency("Octopus Deploy API", _dependencyContext.Value, octopusResponse.Request.Uri.Host, octopusResponse.Request.Uri.PathAndQuery, tracking.startTime, tracking.duration.Elapsed, octopusResponse.StatusCode.ToString(), octopusResponse.StatusCode == HttpStatusCode.OK);
            }
            _dependencyContext.Value = string.Empty;
        }
    }
}