using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusClientFactory : IOctopusClientFactory
    {
        private readonly IAppInsights _appInsights;
        private readonly IDictionary<string, OctopusInstance> _instances = new ConcurrentDictionary<string, OctopusInstance>();
        private readonly ILogger _logger;
        private readonly Timer _metricTimer;

        public OctopusClientFactory(ILogger logger, IAppInsights appInsights)
        {
            _logger = logger;
            _appInsights = appInsights;
            _metricTimer = new Timer(MetricTimerHandler, null, TimeSpan.Zero, TimeSpan.FromMinutes(15));
        }

        public void Reset()
        {
            lock (_instances)
            {
                foreach (var instance in _instances)
                    instance.Value.Dispose();
                _instances.Clear();
            }
        }

        public int RegisteredOctopusServers => _instances.Count;

        public bool IsAuthenticated(OctopusCredential credential)
        {
            return GetInstance(credential).IsAuthenticated;
        }

        public IOctopusConnection GetConnection(OctopusCredential credential)
        {
            return GetInstance(credential).Connection;
        }

        public IOctopusServer GetServer(OctopusCredential credential)
        {
            return GetInstance(credential).Server;
        }

        private void MetricTimerHandler(object state)
        {
            try
            {
                foreach (var repo in _instances.Where(repo => repo.Value.Connection.IsAuthenticated))
                {
                    _appInsights.TrackMetric("MemoryCache - Approximate Size", repo.Value.Server.CacheSize);
                    _appInsights.TrackMetric("MemoryCache - Server Entries", repo.Value.Server.CachedItems);
                    _appInsights.TrackMetric("MemoryCache - Server Preload Entries", repo.Value.Server.PreloadCount);
                }
            }
            catch (Exception e)
            {
                _logger.Exception(e);
            }
        }


        private OctopusInstance GetInstance(OctopusCredential credential)
        {
            var host = credential.GetHost();
            if (_instances.ContainsKey(host) && _instances[host].IsMatch(credential.BaseUri, credential.ApiKey))
                return _instances[host];

            lock (_instances)
            {
                return CreateOctopusInstance(credential);
            }
        }


        private OctopusInstance CreateOctopusInstance(OctopusCredential credential)
        {
            var host = credential.GetHost();

            if (_instances.ContainsKey(host) && _instances[host].IsMatch(credential.BaseUri, credential.ApiKey))
                return _instances[host];

            var connection = new OctopusConnection(_appInsights, _logger, credential.BaseUri, credential.ApiKey);

            _logger.Verbose($"Creating Octopus API Connection: {connection.BaseUri}. IsAuthenticated: {connection.IsAuthenticated}");
            _appInsights.TrackEvent("CreateOctopusInstance", new Dictionary<string, string>
            {
                {"BaseUri", connection.BaseUri},
                {"IsAuthenticated", connection.IsAuthenticated.ToString()}
            });
            if (!connection.IsAuthenticated)
                return null;

            connection.ConfigureAppInsightsDependencyTracking();

            var server = new OctopusServer(connection, _logger);

            var instance = new OctopusInstance(connection, server);

            if (_instances.ContainsKey(host))
            {
                _instances[host].Dispose();
                _instances.Remove(host);
            }
            _instances[instance.Key] = instance;

            return instance;
        }

        private class OctopusInstance : IDisposable
        {
            public OctopusInstance(OctopusConnection connection, OctopusServer server)
            {
                Connection = connection;
                Server = server;
            }

            public string Key => new Uri(Connection.BaseUri).Host;
            public bool IsAuthenticated => Connection.IsAuthenticated;
            public OctopusConnection Connection { get; }
            public OctopusServer Server { get; }

            public void Dispose()
            {
                Server.Dispose();
                Connection.Dispose();
            }

            public bool IsMatch(string baseUrl, string apiKey)
            {
                return new Uri(Connection.BaseUri) == new Uri(baseUrl) && string.Equals(Connection.ApiKey, apiKey);
            }
        }
    }
}