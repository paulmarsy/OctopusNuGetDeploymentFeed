using System;
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
        private readonly object _allProjectSyncLock = new object();
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;
        private readonly OctopusServer _server;
        private readonly Timer _timer;

        public OctopusCache(ILogger logger, OctopusServer server)
        {
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                CompactOnMemoryPressure = true,
                ExpirationScanFrequency = TimeSpan.FromMinutes(10)
            });
            _logger = logger;
            _server = server;
            _timer = new Timer(TimerHandler, null, 0, Timeout.Infinite);
        }

        public byte[] GetJson(Resource resource)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.JsonDocument, resource.Id), entry =>
            {
                entry.Priority = CacheItemPriority.Low;
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
                entry.AbsoluteExpiration = GetNextHourDateTimeOffset();
                return _server.Repository.Projects.FindByName(name);
            });
        }

        public IEnumerable<ProjectResource> GetAllProjects()
        {
            if (!_cache.TryGetValue(CacheKey(CacheKeyType.ProjectList), out IList<ProjectResource> projects))
                lock (_allProjectSyncLock)
                {
                    projects = UpdateProjectCache();
                }
            return projects;
        }

        public ChannelResource GetChannel(string channelId)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.Channel, channelId), entry =>
            {
                entry.Priority = CacheItemPriority.Low;
                return _server.Repository.Channels.Get(channelId);
            });
        }

        public IEnumerable<ReleaseResource> ListReleases(ProjectResource project)
        {
            return from release in _server.Repository.Projects.GetReleases(project).Items
                let version = release.Version.ToSemanticVersion()
                where version != null
                select _cache.Set(CacheKey(CacheKeyType.Release, project.Id, version.ToNormalizedString()), release, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromHours(1)
                });
        }

        public ReleaseResource GetRelease(ProjectResource project, SemanticVersion version)
        {
            if (_cache.TryGetValue(CacheKey(CacheKeyType.Release, project.Id, version.ToNormalizedString()), out ReleaseResource release))
                return release;

            return ListReleases(project).SingleOrDefault(package =>
            {
                var packageVesion = package.Version.ToSemanticVersion();
                return string.Equals(version.ToOriginalString(), packageVesion.ToOriginalString(), StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(version.ToNormalizedString(), packageVesion.ToNormalizedString(), StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(version.ToFullString(), packageVesion.ToFullString(), StringComparison.OrdinalIgnoreCase);
            });
        }

        private static string CacheKey(CacheKeyType type, params string[] id)
        {
            return type + ':' + string.Join(";", id.Select(x => x.ToLowerInvariant()));
        }

        private void TimerHandler(object state)
        {
            try
            {
                lock (_allProjectSyncLock)
                {
                    _cache.Remove(CacheKey(CacheKeyType.ProjectList));
                    UpdateProjectCache();
                }
                _timer.Change(MilliSecondsTilTheHour(), Timeout.Infinite);
            }
            catch (Exception e)
            {
                LogManager.Current.Exception(e);
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
                entry.AbsoluteExpiration = expiration;
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
            Channel
        }
    }
}