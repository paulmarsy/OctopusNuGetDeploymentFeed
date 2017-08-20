using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NuGet;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.Infrastructure;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;
using MemoryCache = Microsoft.Extensions.Caching.Memory.MemoryCache;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusCache : IOctopusCache, IDisposable
    {
        private static readonly IReadOnlyDictionary<CacheKeyType, TimeSpan> CacheTime = new Dictionary<CacheKeyType, TimeSpan>
        {
            {CacheKeyType.NuGetPackage, TimeSpan.FromHours(1)},
            {CacheKeyType.Channel, TimeSpan.FromDays(2)},
            {CacheKeyType.Release, TimeSpan.FromHours(3)},
            {CacheKeyType.JsonDocument, TimeSpan.FromHours(6)},
            {CacheKeyType.ProjectReleases, TimeSpan.FromMinutes(2)} // Cache for pagination calls
        };

        private readonly MemoryCache _cache;
        private readonly SizedReference _cacheSizeRef;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<(CacheKeyType type, string id), (DateTimeOffset lastAccess, DateTimeOffset lastUpdate)> _preloadRegistry;
        private readonly OctopusServer _server;
        private readonly Timer _timer;
        private CancellationTokenSource _projectEvictionTokenSource;

        public OctopusCache(OctopusServer server, ILogger logger)
        {
            _server = server;
            _logger = logger;
            _cache = new MemoryCache(new MemoryCacheOptions());
            _cacheSizeRef = new SizedReference(_cache);
            _preloadRegistry = new ConcurrentDictionary<(CacheKeyType, string), (DateTimeOffset lastAccess, DateTimeOffset lastUpdate)>();
            _timer = new Timer(TimerHandler, null, 0, Timeout.Infinite);
            _projectEvictionTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _timer.Dispose();
            _cache.Dispose();
        }

        public ProjectResource TryGetProject(string name)
        {
            return _cache.Get<ProjectResource>(CacheKey(CacheKeyType.Project, name));
        }

        public int Count => _cache.Count;
        public long ApproximateSize => _cacheSizeRef.ApproximateSize;

        public ReleaseResource GetLatestRelease(ProjectResource project)
        {
            SemanticVersion semver = null;
            ReleaseResource release = null;
            for (var skip = 0; semver == null; skip++)
            {
                release = _server.GetRepository($"GetLatestRelease: {project.Name}").Projects.GetReleases(project, skip, 1).Items.Single();
                semver = release.Version.ToSemanticVersion();
            }

            return _cache.Set(CacheKey(CacheKeyType.Release, project.Id, semver.ToNormalizedString()), release, CacheTime[CacheKeyType.Release]);
        }

        public byte[] GetJson(Resource resource)
        {
            RegisterPreloadAccess(CacheKeyType.JsonDocument, resource.Link("Self"), false);

            return _cache.GetOrCreate(CacheKey(CacheKeyType.JsonDocument, resource.Link("Self")), entry =>
            {
                RegisterPreloadAccess(CacheKeyType.JsonDocument, resource.Link("Self"), true);
                entry.SetAbsoluteExpiration(CacheTime[CacheKeyType.JsonDocument]);
                using (var sourceStream = _server.GetClient($"GetJson: {resource.Link("Self")}").GetContent(resource.Link("Self")))
                {
                    return sourceStream.ReadAllBytes();
                }
            });
        }

        public ProjectResource GetProject(string name)
        {
            return _cache.GetOrCreate(CacheKey(CacheKeyType.Project, name), entry =>
            {
                entry.AddExpirationToken(new CancellationChangeToken(_projectEvictionTokenSource.Token));
                return _server.GetRepository($"GetProject: {name}").Projects.FindByName(name);
            });
        }

        public IEnumerable<ProjectResource> GetAllProjects()
        {
            var currentProjectSet = _projectEvictionTokenSource;

            if (!_cache.TryGetValue(CacheKey(CacheKeyType.ProjectList), out IEnumerable<ProjectResource> projects))
                projects = LoadAllProjects(currentProjectSet);

            return projects;
        }

        public ChannelResource GetChannel(string channelId)
        {
            RegisterPreloadAccess(CacheKeyType.Channel, channelId, false);

            return _cache.GetOrCreate(CacheKey(CacheKeyType.Channel, channelId), entry =>
            {
                RegisterPreloadAccess(CacheKeyType.Channel, channelId, true);
                entry.SetAbsoluteExpiration(CacheTime[CacheKeyType.Channel]);
                return _server.GetRepository($"GetChannel: {channelId}").Channels.Get(channelId);
            });
        }

        public IEnumerable<ReleaseResource> ListReleases(ProjectResource project)
        {
            foreach (var release in _cache.GetOrCreate(CacheKey(CacheKeyType.ProjectReleases, project.Id), entry =>
            {
                entry.SetAbsoluteExpiration(CacheTime[CacheKeyType.ProjectReleases]); // Cache for pagination calls

                return _server.GetRepository($"ListReleases: {project.Name}").Projects.GetAllReleases(project);
            }))
            {
                var semver = release.Version.ToSemanticVersion();
                if (semver == null)
                    continue;

                yield return _cache.Set(CacheKey(CacheKeyType.Release, project.Id, semver.ToNormalizedString()), release, CacheTime[CacheKeyType.Release]);
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
                entry.SetAbsoluteExpiration(CacheTime[CacheKeyType.NuGetPackage]);
                return nugetPackageFactory();
            });
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
                LoadAllProjects(_projectEvictionTokenSource);
                foreach (var entry in _preloadRegistry)
                {
                    _logger.Verbose($"Checking Preload {entry.Key.type}: {entry.Key.id}");
                    TimeSpan preloadDuration;
                    Func<string, object> preloadAction;
                    switch (entry.Key.type)
                    {
                        case CacheKeyType.JsonDocument:
                            preloadDuration = TimeSpan.FromDays(2);
                            preloadAction = resourceUrl =>
                            {
                                using (var sourceStream = _server.GetClient($"Preload JsonDocument: {entry.Key.id}").GetContent(resourceUrl))
                                {
                                    return sourceStream.ReadAllBytes();
                                }
                            };
                            break;
                        case CacheKeyType.Release:
                            preloadDuration = TimeSpan.FromDays(2);
                            preloadAction = id => _server.GetRepository($"Preload Release: {entry.Key.id}").Releases.Get(id);
                            break;
                        case CacheKeyType.Channel:
                            preloadDuration = TimeSpan.FromDays(7);
                            preloadAction = id => _server.GetRepository($"Preload Channel: {entry.Key.id}").Channels.Get(id);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (DateTimeOffset.UtcNow - entry.Value.lastAccess >= preloadDuration)
                        _preloadRegistry.TryRemove(entry.Key, out _);
                    else if (DateTimeOffset.UtcNow - entry.Value.lastUpdate >= CacheTime[entry.Key.type])
                        _cache.Set(CacheKey(entry.Key.type, entry.Key.id), preloadAction(entry.Key.id), CacheTime[entry.Key.type].Add(TimeSpan.FromHours(1)));
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

        private IEnumerable<ProjectResource> LoadAllProjects(CancellationTokenSource currentSet)
        {
            lock (currentSet)
            {
                if (currentSet.IsCancellationRequested || currentSet != _projectEvictionTokenSource)
                    return _cache.Get<IEnumerable<ProjectResource>>(CacheKey(CacheKeyType.ProjectList));

                currentSet.Cancel();

                var projectEvictionTokenSource = new CancellationTokenSource();
                var projectList = _server.GetRepository("LoadAllProjects").Projects.GetAll().ToArray();

                _cache.Set(CacheKey(CacheKeyType.ProjectList), projectList, new CancellationChangeToken(projectEvictionTokenSource.Token));
                foreach (var project in projectList)
                    _cache.Set(CacheKey(CacheKeyType.Project, project.Name), project, new CancellationChangeToken(projectEvictionTokenSource.Token));

                _projectEvictionTokenSource = projectEvictionTokenSource;

                return projectList;
            }
        }
    }
}