using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusPackageRepositoryFactory : IPackageRepositoryFactory
    {
        private readonly IAppInsights _appInsights;
        private readonly IDictionary<string, OctopusInstance> _instances = new ConcurrentDictionary<string, OctopusInstance>();
        private readonly ILogger _logger;
        private readonly Timer _metricTimer;

        public OctopusPackageRepositoryFactory(ILogger logger, IAppInsights appInsights)
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

        public int Count => _instances.Count;

        public bool IsAuthenticated(string username, string password)
        {
            return GetInstance(GetHost(username), username, password).IsAuthenticated;
        }

        public IOctopusServer GetServer(IPrincipal user)
        {
            return GetInstance(user).Server;
        }

        public IOctopusCache GetCache(IPrincipal user)
        {
            return GetInstance(user).Cache;
        }

        public IPackageRepository GetPackageRepository(IPrincipal user)
        {
            return GetInstance(user).Repository;
        }

        private static string GetHost(string value)
        {
            try
            {
                return new Uri(value).Host;
            }
            catch
            {
                return null;
            }
        }

        private void MetricTimerHandler(object state)
        {
            try
            {
                foreach (var repo in _instances.Where(repo => repo.Value.Server.IsAuthenticated))
                {
                    _appInsights.TrackMetric("MemoryCache - Approximate Size", repo.Value.Cache.ApproximateSize);
                    _appInsights.TrackMetric("MemoryCache - Cache Entries", repo.Value.Cache.Count);

                    var totalRequests = repo.Value.Repository.Requests;
                    var misses = repo.Value.Server.Requests;
                    var hits = totalRequests - misses;

                    _appInsights.TrackMetric("MemoryCache - Cache Hits", hits);
                    _appInsights.TrackMetric("MemoryCache - Cache Misses", misses);
                    _appInsights.TrackMetric("MemoryCache - Cache Hit Ratio", hits / (double) totalRequests);
                    _appInsights.TrackMetric("MemoryCache - Cache Preload Updates", repo.Value.Cache.Preloads);
                    _appInsights.TrackMetric("MemoryCache - Cache Preload Entries", repo.Value.Cache.PreloadCount);
                }
            }
            catch (Exception e)
            {
                _logger.Exception(e);
            }
        }

        private OctopusInstance GetInstance(IPrincipal user)
        {
            var context = GetOctopusContext(user);
            return GetInstance(context.host, context.baseUrl, context.apiKey);
        }

        private OctopusInstance GetInstance(string host, string baseUrl, string apiKey)
        {
            if (_instances.ContainsKey(host) && _instances[host].IsMatch(baseUrl, apiKey))
                return _instances[host];

            lock (_instances)
            {
                return CreateOctopusRepository(host, baseUrl, apiKey);
            }
        }

        private static (string host, string baseUrl, string apiKey) GetOctopusContext(IPrincipal user)
        {
            var claimsPrincipal = user as ClaimsPrincipal;
            var baseUri = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.Uri)?.Value;
            var apiKey = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.UserData)?.Value;

            return (GetHost(baseUri), baseUri, apiKey);
        }

        private OctopusInstance CreateOctopusRepository(string host, string baseUrl, string apiKey)
        {
            if (_instances.ContainsKey(host))
            {
                if (_instances[host].IsMatch(baseUrl, apiKey))
                    return _instances[host];

                _instances[host].Dispose();
                _instances.Remove(host);
            }
            var server = new OctopusServer(_appInsights, _logger, baseUrl, apiKey);

            var authenticated = server.IsAuthenticated;
            _logger.Info($"Creating Octopus API Connection: {server.BaseUri}. IsAuthenticated: {authenticated}");
            _appInsights.TrackEvent("CreateOctopusRepository", new Dictionary<string, string>
            {
                {"BaseUri", server.BaseUri},
                {"IsAuthenticated", authenticated.ToString()}
            });
            if (!server.IsAuthenticated)
                return null;

            server.ConfigureAppInsightsDependencyTracking();

            var cache = new OctopusCache(server, _logger);
            var repository = new OctopusPackageRepository(_logger, server, cache);
            var instance = new OctopusInstance(server, cache, repository);
            _instances[instance.Key] = instance;

            return instance;
        }

        private class OctopusInstance : IDisposable
        {
            public OctopusInstance(OctopusServer server, OctopusCache cache, OctopusPackageRepository repository)
            {
                Server = server;
                Cache = cache;
                Repository = repository;
            }

            public string Key => new Uri(Server.BaseUri).Host;
            public bool IsAuthenticated => Server.IsAuthenticated;
            public OctopusServer Server { get; }
            public OctopusCache Cache { get; }
            public OctopusPackageRepository Repository { get; }

            public void Dispose()
            {
                Cache.Dispose();
                Server.Dispose();
            }

            public bool IsMatch(string baseUrl, string apiKey)
            {
                return new Uri(Server.BaseUri) == new Uri(baseUrl) && string.Equals(Server.ApiKey, apiKey);
            }
        }
    }
}