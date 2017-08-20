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
        private readonly IDictionary<string, OctopusInstance> _allRepos = new ConcurrentDictionary<string, OctopusInstance>();
        private readonly IAppInsights _appInsights;
        private readonly ILogger _logger;
        private readonly Timer _metricTimer;

        public OctopusPackageRepositoryFactory(ILogger logger, IAppInsights appInsights)
        {
            _logger = logger;
            _appInsights = appInsights;
            _metricTimer = new Timer(MetricTimerHandler, null, Timeout.InfiniteTimeSpan, TimeSpan.FromMinutes(10));
        }

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
            foreach (var repo in _allRepos.Where(repo => repo.Value.Server.IsAuthenticated))
            {
                _appInsights.TrackMetric("MemoryCache - Approximate Size", repo.Value.Cache.ApproximateSize);
                _appInsights.TrackMetric("MemoryCache - Cache Entries", repo.Value.Cache.Count);

                var totalRequests = repo.Value.Repository.Requests;
                var misses = repo.Value.Server.Requests;
                var hits = totalRequests - misses;

                _appInsights.TrackMetric("MemoryCache - Cache Hits", hits);
                _appInsights.TrackMetric("MemoryCache - Cache Misses", misses);
                _appInsights.TrackMetric("MemoryCache - Cache Hit Ratio", hits / (double) totalRequests);
            }
        }

        private OctopusInstance GetInstance(IPrincipal user)
        {
            var context = GetOctopusContext(user);
            return GetInstance(context.host, context.baseUrl, context.apiKey);
        }

        private OctopusInstance GetInstance(string host, string baseUrl, string apiKey)
        {
            if (_allRepos.ContainsKey(host) && _allRepos[host].IsMatch(baseUrl, apiKey))
                return _allRepos[host];

            lock (_allRepos)
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
            if (_allRepos.ContainsKey(host))
            {
                if (_allRepos[host].IsMatch(baseUrl, apiKey))
                    return _allRepos[host];

                _allRepos[host].Dispose();
                _allRepos.Remove(host);
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
            _allRepos[host] = instance;

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