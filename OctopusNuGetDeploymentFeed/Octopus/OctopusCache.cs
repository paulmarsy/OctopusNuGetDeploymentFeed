using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using NuGet;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Logging;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;
using MemoryCache = Microsoft.Extensions.Caching.Memory.MemoryCache;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusCache : IOctopusCache
    {
        private readonly object _allProjectPreloadLock = new object();
        private readonly IAppInsights _appInsights;
        private readonly MemoryCache _cache;
        private readonly ILogger _logger;
        private readonly Timer _metricTimer;
        private readonly ConcurrentDictionary<(CacheKeyType type, string id), (DateTimeOffset lastAccess, DateTimeOffset lastUpdate)> _preloadRegistry;
        private readonly OctopusServer _server;
        private readonly Timer _timer;

        public OctopusCache(OctopusServer server, ILogger logger, MemoryCache cache, IAppInsights appInsights)
        {
            _server = server;
            _logger = logger;
            _cache = cache;
            _appInsights = appInsights;
            _preloadRegistry = new ConcurrentDictionary<(CacheKeyType, string), (DateTimeOffset lastAccess, DateTimeOffset lastUpdate)>();
            _timer = new Timer(TimerHandler, null, 0, Timeout.Infinite);
            _metricTimer = new Timer(MetricTimerHandler, null, Timeout.InfiniteTimeSpan, TimeSpan.FromMinutes(10));
        }

        public ProjectResource TryGetProject(string name)
        {
            return _cache.Get<ProjectResource>(CacheKey(CacheKeyType.Project, name));
        }

        public ReleaseResource GetLatestRelease(ProjectResource project)
        {
            SemanticVersion semver = null;
            ReleaseResource release = null;
            for (var skip = 0; semver == null; skip++)
            {
                release = _server.GetRepository($"GetLatestRelease {project.Name}").Projects.GetReleases(project, skip, 1).Items.Single();
                semver = release.Version.ToSemanticVersion();
            }

            return _cache.Set(CacheKey(CacheKeyType.Release, project.Id, semver.ToNormalizedString()), release, TimeSpan.FromHours(1));
        }

        public byte[] GetJson(Resource resource)
        {
            RegisterPreloadAccess(CacheKeyType.JsonDocument, resource.Link("Self"), false);

            return _cache.GetOrCreate(CacheKey(CacheKeyType.JsonDocument, resource.Link("Self")), entry =>
            {
                RegisterPreloadAccess(CacheKeyType.JsonDocument, resource.Link("Self"), true);
                entry.SetAbsoluteExpiration(TimeSpan.FromHours(3));
                using (var sourceStream = _server.GetClient($"GetJson {resource.Id}").GetContent(resource.Link("Self")))
                {
                    return sourceStream.ReadAllBytes();
                }
            });
        }

        public ProjectResource GetProject(string name)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.Project, name), entry =>
            {
                entry.SetAbsoluteExpiration(GetNextHourDateTimeOffset());
                return _server.GetRepository($"GetProject {name}").Projects.FindByName(name);
            });
        }

        public IEnumerable<ProjectResource> GetAllProjects()
        {
            if (_cache.TryGetValue(CacheKey(CacheKeyType.ProjectList), out IEnumerable<ProjectResource> projects))
                return projects;

            lock (_allProjectPreloadLock)
            {
                return LoadAllProjects();
            }
        }

        public ChannelResource GetChannel(string channelId)
        {
            RegisterPreloadAccess(CacheKeyType.Channel, channelId, false);

            return _cache.GetOrCreate(CacheKey(CacheKeyType.Channel, channelId), entry =>
            {
                RegisterPreloadAccess(CacheKeyType.Channel, channelId, true);
                entry.SetAbsoluteExpiration(TimeSpan.FromDays(1));
                return _server.GetRepository($"GetChannel {channelId}").Channels.Get(channelId);
            });
        }

        public IEnumerable<ReleaseResource> ListReleases(ProjectResource project)
        {
            foreach (var release in _cache.GetOrCreate(CacheKey(CacheKeyType.ProjectReleases, project.Id), entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(3));

                return _server.GetRepository($"ListReleases {project.Name}").Projects.GetAllReleases(project);
            }))
            {
                var semver = release.Version.ToSemanticVersion();
                if (semver == null)
                    continue;

                yield return _cache.Set(CacheKey(CacheKeyType.Release, project.Id, semver.ToNormalizedString()), release, TimeSpan.FromHours(1));
            }
        }

        public ReleaseResource GetRelease(ProjectResource project, SemanticVersion semver)
        {
            if (!_cache.TryGetValue(CacheKey(CacheKeyType.Release, project.Id, semver.ToNormalizedString()), out ReleaseResource release))
            {
                // Try for an exact match, failing that go for a normalized match
                var allReleases = ListReleases(project).ToArray();
                release = allReleases.SingleOrDefault(package => string.Equals(semver.ToOriginalString(), package.Version, StringComparison.OrdinalIgnoreCase)) ??
                          allReleases.FirstOrDefault(package => string.Equals(semver.ToNormalizedString(), package.Version.ToSemanticVersion().ToNormalizedString(), StringComparison.OrdinalIgnoreCase));
            }
            if (release == null)
                return null;

            RegisterPreloadAccess(CacheKeyType.Release, release.Id, false);
            return release;
        }

        public byte[] GetNuGetPackage(ProjectResource project, ReleaseResource release, Func<byte[]> nugetPackageFactory)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.NuGetPackage, project.Id, release.Id), entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromHours(1));
                return nugetPackageFactory();
            });
        }

        private void MetricTimerHandler(object state)
        {
            _appInsights.TrackMetric("MemoryCache - # Cached Items", _cache.Count);
        }

        private void RegisterPreloadAccess(CacheKeyType type, string id, bool updated)
        {
            var lastUpdate = _preloadRegistry.ContainsKey((type, id)) && !updated ? _preloadRegistry[(type, id)].lastUpdate : DateTimeOffset.UtcNow;
            _preloadRegistry[(type, id)] = (lastAccess: DateTimeOffset.UtcNow, lastUpdate: lastUpdate);
        }

        private static string CacheKey(CacheKeyType type, params string[] id)
        {
            return $"{type}:{string.Join(";", id.Select(x => x.ToLowerInvariant()))}";
        }

        private void TimerHandler(object state)
        {
            try
            {
                lock (_allProjectPreloadLock)
                {
                    LoadAllProjects();
                }
                foreach (var entry in _preloadRegistry)
                {
                    TimeSpan preloadDuration;
                    TimeSpan updateInterval;
                    Func<string, object> preloadAction;
                    switch (entry.Key.type)
                    {
                        case CacheKeyType.JsonDocument:
                            preloadDuration = TimeSpan.FromDays(2);
                            updateInterval = TimeSpan.FromHours(3);
                            preloadAction = resourceUrl =>
                            {
                                using (var sourceStream = _server.GetClient("Preload JsonDocument").GetContent(resourceUrl))
                                {
                                    return sourceStream.ReadAllBytes();
                                }
                            };
                            break;
                        case CacheKeyType.Release:
                            preloadDuration = TimeSpan.FromDays(2);
                            updateInterval = TimeSpan.FromDays(1);
                            preloadAction = id => _server.GetRepository("Preload Release").Releases.Get(id);
                            break;
                        case CacheKeyType.Channel:
                            preloadDuration = TimeSpan.FromDays(7);
                            updateInterval = TimeSpan.FromDays(1);
                            preloadAction = id => _server.GetRepository("Preload Channel").Channels.Get(id);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (DateTimeOffset.UtcNow - entry.Value.lastAccess >= preloadDuration)
                        _preloadRegistry.TryRemove(entry.Key, out _);
                    else if (DateTimeOffset.UtcNow - entry.Value.lastUpdate >= updateInterval)
                        _cache.Set(CacheKey(entry.Key.type, entry.Key.id), preloadAction(entry.Key.id), updateInterval);
                }
            }
            catch (Exception e)
            {
                _logger.Exception(e);
            }
            finally
            {
                _timer.Change(MilliSecondsTilTheHour(), Timeout.Infinite);
            }
        }

        private static int MilliSecondsTilTheHour()
        {
            var now = DateTimeOffset.Now;
            var minutesRemaining = 59 - now.Minute;
            var secondsRemaining = 59 - now.Second;
            var interval = (minutesRemaining * 60 + secondsRemaining) * 1000;

            if (interval == 0)
                interval = 60 * 60 * 1000;

            return interval;
        }

        private static DateTimeOffset GetNextHourDateTimeOffset()
        {
            var now = DateTimeOffset.Now;
            return now.Date.AddHours(now.Hour).AddHours(1);
        }

        private IEnumerable<ProjectResource> LoadAllProjects()
        {
            var projectList = _server.GetRepository("LoadAllProjects").Projects.GetAll().ToArray();

            _cache.Set(CacheKey(CacheKeyType.ProjectList), projectList);
            foreach (var project in projectList)
                _cache.Set(CacheKey(CacheKeyType.Project, project.Name), project, GetNextHourDateTimeOffset());

            return projectList;
        }
    }
}