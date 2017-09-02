using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public async Task<bool> IsAuthenticated(OctopusCredential credential)
        {
            var instance = GetInstance(credential) ?? await CreateOctopusInstance(credential);
            return await instance.IsAuthenticated();
        }

        public IOctopusConnection GetConnection(OctopusCredential credential)
        {
            return (GetInstance(credential) ?? CreateOctopusInstance(credential).GetAwaiter().GetResult()).Connection;
        }

        public IOctopusServer GetServer(OctopusCredential credential)
        {
            return (GetInstance(credential) ?? CreateOctopusInstance(credential).GetAwaiter().GetResult()).Server;
        }

        private void MetricTimerHandler(object state)
        {
            try
            {
                foreach (var repo in _instances.Where(repo => repo.Value.Connection.IsAuthenticated().GetAwaiter().GetResult()))
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
            lock (_instances)
            {
                if (_instances.ContainsKey(host) && _instances[host].IsMatch(credential.BaseUri, credential.ApiKey))
                    return _instances[host];
                return null;
            }
        }

        private async Task<OctopusInstance> CreateOctopusInstance(OctopusCredential credential)
        {
            var host = credential.GetHost();

            var connection = new OctopusConnection(_appInsights, _logger, credential.BaseUri, credential.ApiKey);
            var isAuthenticated = await connection.IsAuthenticated();
            _logger.Verbose($"Creating Octopus API Connection: {connection.BaseUri}. IsAuthenticated: {isAuthenticated}");
            _appInsights.TrackEvent("CreateOctopusInstance", new Dictionary<string, string>
            {
                {"BaseUri", connection.BaseUri},
                {"IsAuthenticated", isAuthenticated.ToString()}
            });
            if (!isAuthenticated)
                return null;

            connection.ConfigureAppInsightsDependencyTracking();

            var server = new OctopusServer(connection, _logger);

            var instance = new OctopusInstance(connection, server);
            lock (_instances)
            {
                if (_instances.ContainsKey(host))
                {
                    _instances[host].Dispose();
                    _instances.Remove(host);
                }
                _instances[instance.Key] = instance;
            }

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
            public OctopusConnection Connection { get; }
            public OctopusServer Server { get; }

            public void Dispose()
            {
                Server.Dispose();
                Connection.Dispose();
            }

            public async Task<bool> IsAuthenticated()
            {
                return await Connection.IsAuthenticated();
            }

            public bool IsMatch(string baseUrl, string apiKey)
            {
                return new Uri(Connection.BaseUri) == new Uri(baseUrl) && string.Equals(Connection.ApiKey, apiKey);
            }
        }
    }
}