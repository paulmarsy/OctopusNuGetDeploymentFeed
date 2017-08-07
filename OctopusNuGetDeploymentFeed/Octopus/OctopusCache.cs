using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using NuGet;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.Infrastructure;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;
using MemoryCache = Microsoft.Extensions.Caching.Memory.MemoryCache;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusCache : IOctopusCache
    {
        private readonly object _allProjectSyncLock = new object();
        private readonly IAppInsights _appInsights;
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;
        private readonly OctopusServer _server;
        private readonly Timer _timer;

        public OctopusCache(OctopusServer server, IAppInsights appInsights, ILogger logger)
        {
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                CompactOnMemoryPressure = true,
                ExpirationScanFrequency = TimeSpan.FromMinutes(10)
            });
            _server = server;
            _appInsights = appInsights;
            _logger = logger;
            _timer = new Timer(TimerHandler, null, 0, Timeout.Infinite);
        }

        public byte[] GetJson(Resource resource)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.JsonDocument, resource.Id), entry =>
            {
                TrackCacheEvent(CacheKeyType.JsonDocument, resource.Id);
                entry.SetPriority(CacheItemPriority.Low);
                using (var sourceStream = _server.Client.GetContent(resource.Link("Self")))
                {
                    return sourceStream.ReadAllBytes();
                }
            });
        }

        public ProjectResource GetProject(string name)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.Project, name), entry =>
            {
                TrackCacheEvent(CacheKeyType.Project, name);
                entry.SetAbsoluteExpiration(GetNextHourDateTimeOffset());
                return _server.Repository.Projects.FindByName(name);
            });
        }

        public IEnumerable<ProjectResource> GetAllProjects()
        {
            if (!_cache.TryGetValue(CacheKey(CacheKeyType.ProjectList), out IList<ProjectResource> projects))
                lock (_allProjectSyncLock)
                {
                    TrackCacheEvent(CacheKeyType.ProjectList, "All");
                    projects = UpdateProjectCache();
                }
            return projects;
        }

        public ChannelResource GetChannel(string channelId)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.Channel, channelId), entry =>
            {
                TrackCacheEvent(CacheKeyType.Channel, channelId);
                entry.SetPriority(CacheItemPriority.Low);
                return _server.Repository.Channels.Get(channelId);
            });
        }

        public IEnumerable<ReleaseResource> ListReleases(ProjectResource project)
        {
            foreach (var release in _server.Repository.Projects.GetReleases(project).Items)
            {
                var version = release.Version.ToSemanticVersion();
                if (version == null)
                {
                    _appInsights.TrackEvent("SemanticVersion.ParseError", new Dictionary<string, string>
                    {
                        {"Project", project.Name},
                        {"Release", release.Version}
                    });
                    continue;
                }
                TrackCacheEvent(CacheKeyType.Release, project.Id + ";" + version.ToNormalizedString(), "Seeded");
                yield return _cache.Set(CacheKey(CacheKeyType.Release, project.Id, version.ToNormalizedString()), release, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromHours(1)
                });
            }
        }

        public ReleaseResource GetRelease(ProjectResource project, SemanticVersion version)
        {
            if (_cache.TryGetValue(CacheKey(CacheKeyType.Release, project.Id, version.ToNormalizedString()), out ReleaseResource release))
                return release;

            TrackCacheEvent(CacheKeyType.Release, project.Id + ";" + version.ToNormalizedString());
            return ListReleases(project).SingleOrDefault(package =>
            {
                var packageVesion = package.Version.ToSemanticVersion();
                return string.Equals(version.ToOriginalString(), packageVesion.ToOriginalString(), StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(version.ToNormalizedString(), packageVesion.ToNormalizedString(), StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(version.ToFullString(), packageVesion.ToFullString(), StringComparison.OrdinalIgnoreCase);
            });
        }

        public byte[] GetNuGetPackage(ProjectResource project, ReleaseResource release, Func<byte[]> nugetPackageFactory)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.NuGetPackage, project.Id, release.Id), entry =>
            {
                TrackCacheEvent(CacheKeyType.NuGetPackage, project.Id + ";" + release.Id);
                entry.SetSlidingExpiration(TimeSpan.FromHours(1));
                return nugetPackageFactory();
            });
        }

        private static string CacheKey(CacheKeyType type, params string[] id)
        {
            return type + ':' + string.Join(";", id.Select(x => x.ToLowerInvariant()));
        }

        private void TrackCacheEvent(CacheKeyType type, string id, string eventName = "Miss")
        {
            _appInsights.TrackEvent($"MemoryCache {eventName}", new Dictionary<string, string>
            {
                {"Cache Entry Type", type.ToString()},
                {"Cache Key", id}
            });
        }

        private void TimerHandler(object state)
        {
            try
            {
                TrackCacheEvent(CacheKeyType.ProjectList, "All", "Seeded");
                lock (_allProjectSyncLock)
                {
                    _cache.Remove(CacheKey(CacheKeyType.ProjectList));
                    UpdateProjectCache();
                }
                _timer.Change(MilliSecondsTilTheHour(), Timeout.Infinite);
            }
            catch (Exception e)
            {
                _logger.Exception(e);
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

        private IList<ProjectResource> UpdateProjectCache()
        {
            return UpdateProjectCacheImpl().ToList();
        }

        private IEnumerable<ProjectResource> UpdateProjectCacheImpl()
        {
            var expiration = GetNextHourDateTimeOffset();

            foreach (var project in _cache.GetOrCreate(CacheKey(CacheKeyType.ProjectList), entry =>
            {
                entry.SetAbsoluteExpiration(expiration);
                return _server.Repository.Projects.GetAll();
            }))
                yield return _cache.Set(project.Name, project, expiration);
        }

        private enum CacheKeyType
        {
            JsonDocument,
            ProjectList,
            Project,
            Release,
            Channel,
            NuGetPackage
        }
    }
}