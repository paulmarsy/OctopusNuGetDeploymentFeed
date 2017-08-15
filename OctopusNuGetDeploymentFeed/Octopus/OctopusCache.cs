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
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;
        private readonly OctopusServer _server;
        private readonly Timer _timer;

        public OctopusCache(OctopusServer server, ILogger logger)
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _server = server;
            _logger = logger;
            _timer = new Timer(TimerHandler, null, 0, Timeout.Infinite);
        }

        public byte[] GetJson(Resource resource)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.JsonDocument, resource.Id), entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromDays(1));
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
                entry.SetAbsoluteExpiration(GetNextHourDateTimeOffset());
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
                entry.SetAbsoluteExpiration(TimeSpan.FromDays(1));
                return _server.Repository.Channels.Get(channelId);
            });
        }

        public IEnumerable<ReleaseResource> ListReleases(ProjectResource project)
        {
            foreach (var release in _server.Repository.Projects.GetAllReleases(project))
            {
                var semver = release.Version.ToSemanticVersion();
                if (semver == null)
                    continue;

                yield return _cache.Set(CacheKey(CacheKeyType.Release, project.Id, semver.ToNormalizedString()), release, TimeSpan.FromHours(1));
            }
        }

        public ReleaseResource GetRelease(ProjectResource project, SemanticVersion semver)
        {
            var version = semver.ToNormalizedString();
            if (_cache.TryGetValue(CacheKey(CacheKeyType.Release, project.Id, version), out ReleaseResource release))
                return release;

            return ListReleases(project).SingleOrDefault(package => string.Equals(version, package.Version.ToSemanticVersion().ToNormalizedString(), StringComparison.OrdinalIgnoreCase) ||
                                                                    string.Equals(semver.ToOriginalString(), package.Version, StringComparison.OrdinalIgnoreCase));
        }

        public byte[] GetNuGetPackage(ProjectResource project, ReleaseResource release, Func<byte[]> nugetPackageFactory)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.NuGetPackage, project.Id, release.Id), entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromHours(1));
                return nugetPackageFactory();
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