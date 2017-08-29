using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NuGet;
using Octopus.Client.Model;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;
using MemoryCache = Microsoft.Extensions.Caching.Memory.MemoryCache;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    [SuppressMessage("ReSharper", "RedundantAnonymousTypePropertyName", Justification = "Explicitly defined to match REST parameter name")]
    public class OctopusServer : IOctopusServer, IDisposable
    {
        private static readonly IReadOnlyDictionary<CacheEntryType, TimeSpan> CachePreloadTime = new Dictionary<CacheEntryType, TimeSpan>
        {
            {CacheEntryType.ProjectList, TimeSpan.MaxValue},
            {CacheEntryType.Project, TimeSpan.MaxValue},
            {CacheEntryType.Release, TimeSpan.FromDays(3)},
            {CacheEntryType.Channel, TimeSpan.FromDays(5)},
            {CacheEntryType.JsonDocument, TimeSpan.FromDays(3)}
        };

        private static readonly IReadOnlyDictionary<CacheEntryType, TimeSpan> CacheTime = new Dictionary<CacheEntryType, TimeSpan>
        {
            {CacheEntryType.ProjectList, TimeSpan.FromHours(2)},
            {CacheEntryType.Project, TimeSpan.FromHours(2)},
            {CacheEntryType.ReleaseList, TimeSpan.FromMinutes(3)}, // Cache for pagination calls
            {CacheEntryType.Release, TimeSpan.FromDays(2)},
            {CacheEntryType.Channel, TimeSpan.FromDays(3)},
            {CacheEntryType.JsonDocument, TimeSpan.FromDays(2)},
            {CacheEntryType.NuGetPackage, TimeSpan.FromHours(1)}
        };

        private readonly MemoryCache _cache;
        private readonly SizedReference _cacheSizeRef;
        private readonly ILogger _logger;
        private readonly OctopusConnection _octopus;
        private readonly ConcurrentDictionary<string, (DateTimeOffset lastAccess, DateTimeOffset lastUpdate, CacheEntryType type, object state)> _preloadRegistry = new ConcurrentDictionary<string, (DateTimeOffset lastAccess, DateTimeOffset lastUpdate, CacheEntryType type, object state)>();
        private readonly Timer _timer;
        private CancellationTokenSource _projectEvictionTokenSource;

        public OctopusServer(OctopusConnection octopusConnection, ILogger logger)
        {
            _octopus = octopusConnection;
            _logger = logger;
            _cache = new MemoryCache(new MemoryCacheOptions());
            _cacheSizeRef = new SizedReference(_cache);
            _projectEvictionTokenSource = new CancellationTokenSource();
            _timer = new Timer(TimerHandler, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            _timer.Dispose();
            _cache.Dispose();
        }

        public int PreloadCount => _preloadRegistry.Count;

        public ProjectResource TryGetProject(string name)
        {
            return _cache.Get<ProjectResource>(CacheKey(CacheEntryType.Project, name));
        }

        public int CachedItems => _cache.Count;
        public long CacheSize => _cacheSizeRef.ApproximateSize;

        public ReleaseResource GetLatestRelease(ProjectResource project)
        {
            SemanticVersion semver = null;
            ReleaseResource release = null;
            for (var skip = 0; semver == null; skip++)
            {
                release = _octopus.GetClient("Get Latest Release", project.Name).List<ReleaseResource>(project.Link("Releases"), new {skip = skip, take = 1}).Items.FirstOrDefault();
                if (release == null)
                    return null;
                semver = release.Version.ToSemanticVersion();
            }

            RegisterPreloadAccess(CacheEntryType.Release, true, (project, release.Version), project.Id, semver.ToNormalizedString());
            return _cache.Set(CacheKey(CacheEntryType.Release, project.Id, semver.ToNormalizedString()), release, CacheTime[CacheEntryType.Release]);
        }

        public string GetJson(Resource resource)
        {
            var jsonUrl = resource.Link("Self");
            RegisterPreloadAccess(CacheEntryType.JsonDocument, false, jsonUrl, jsonUrl);

            return _cache.GetOrCreate(CacheKey(CacheEntryType.JsonDocument, jsonUrl), entry =>
            {
                RegisterPreloadAccess(CacheEntryType.JsonDocument, true, jsonUrl, jsonUrl);
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.JsonDocument]);
                using (var sourceStream = _octopus.GetClient("Get Json Document", jsonUrl).GetContent(jsonUrl))
                using (var streamReader = new StreamReader(sourceStream, Encoding.UTF8, false))
                {
                    return streamReader.ReadToEnd();
                }
            });
        }

        public ProjectResource GetProject(string name)
        {
            return _cache.GetOrCreate(CacheKey(CacheEntryType.Project, name), entry =>
            {
                entry.AddExpirationToken(new CancellationChangeToken(_projectEvictionTokenSource.Token));
                return _octopus.GetRepository("Find Project", name).Projects.FindByName(name);
            });
        }

        public IEnumerable<ProjectResource> GetAllProjects()
        {
            if (!_cache.TryGetValue(CacheKey(CacheEntryType.ProjectList), out IEnumerable<ProjectResource> projects))
            {
                TimerHandler(null);
                projects = _cache.Get<IEnumerable<ProjectResource>>(CacheKey(CacheEntryType.ProjectList));
                Enumerable.Empty<ProjectResource>();
            }

            return projects;
        }

        public ChannelResource GetChannel(string channelId)
        {
            RegisterPreloadAccess(CacheEntryType.Channel, false, channelId, channelId);

            return _cache.GetOrCreate(CacheKey(CacheEntryType.Channel, channelId), entry =>
            {
                RegisterPreloadAccess(CacheEntryType.Channel, true, channelId, channelId);
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.Channel]);
                return _octopus.GetRepository("Get Channel", channelId).Channels.Get(channelId);
            });
        }

        public IEnumerable<ReleaseResource> ListReleases(ProjectResource project)
        {
            foreach (var release in _cache.GetOrCreate(CacheKey(CacheEntryType.ReleaseList, project.Id), entry =>
            {
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.ReleaseList]); // Cache for pagination calls

                return _octopus.GetRepository("Get All Releases", project.Name).Projects.GetAllReleases(project);
            }))
            {
                var semver = release.Version.ToSemanticVersion();
                if (semver == null)
                    continue;

                yield return _cache.Set(CacheKey(CacheEntryType.Release, project.Id, semver.ToNormalizedString()), release, CacheTime[CacheEntryType.Release]);
            }
        }

        public ReleaseResource GetRelease(ProjectResource project, SemanticVersion semver)
        {
            var updated = false;
            if (!_cache.TryGetValue(CacheKey(CacheEntryType.Release, project.Id, semver.ToNormalizedString()), out ReleaseResource release))
            {
                // Try for an exact match, failing that go for a normalized match
                var allReleases = ListReleases(project).ToArray();
                release = allReleases.SingleOrDefault(package => string.Equals(semver.ToOriginalString(), package.Version, StringComparison.OrdinalIgnoreCase)) ??
                          allReleases.FirstOrDefault(package => string.Equals(semver.ToNormalizedString(), package.Version.ToSemanticVersion().ToNormalizedString(), StringComparison.OrdinalIgnoreCase));
                updated = true;
            }
            if (release == null)
                return null;

            RegisterPreloadAccess(CacheEntryType.Release, updated, (project, release.Version), project.Id, semver.ToNormalizedString());
            return release;
        }

        public byte[] GetNuGetPackage(ProjectResource project, ReleaseResource release, Func<byte[]> nugetPackageFactory)
        {
            return _cache.GetOrCreate(CacheKey(CacheEntryType.NuGetPackage, project.Id, release.Id), entry =>
            {
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.NuGetPackage]);
                return nugetPackageFactory();
            });
        }

        public void InitialisePreloader()
        {
            if (_preloadRegistry.ContainsKey(CacheKey(CacheEntryType.ProjectList)))
                return;
            RegisterPreloadAccess(CacheEntryType.ProjectList, false, null);
            _preloadRegistry.RemoveAll(entry => entry.Value.type == CacheEntryType.Project);
            TimerHandler(null);
        }

        public void InitialisePreloader(ProjectResource project)
        {
            if (_preloadRegistry.ContainsKey(CacheKey(CacheEntryType.ProjectList)) || _preloadRegistry.ContainsKey(CacheKey(CacheEntryType.Project, project.Name)))
                return;
            RegisterPreloadAccess(CacheEntryType.Project, false, project.Id, project.Name);
            TimerHandler(null);
        }

        private void RegisterPreloadAccess(CacheEntryType type, bool updated, object state, params string[] id)
        {
            var lastUpdate = _preloadRegistry.ContainsKey(CacheKey(type, id)) ? _preloadRegistry[CacheKey(type, id)].lastUpdate : DateTimeOffset.MinValue;
            if (updated)
                lastUpdate = DateTimeOffset.UtcNow;

            _preloadRegistry[CacheKey(type, id)] = (lastAccess: DateTimeOffset.UtcNow, lastUpdate: lastUpdate, type: type, state: state);
        }

        private static string CacheKey(CacheEntryType type, params string[] id)
        {
            return $"{type}:{string.Join(";", id.Select(x => x.ToLowerInvariant()))}";
        }

        private void TimerHandler(object state)
        {
            try
            {
                foreach (var entry in _preloadRegistry)
                {
                    _logger.Verbose($"Checking Preload: {entry.Key}");
                    if (DateTimeOffset.UtcNow - entry.Value.lastAccess >= CachePreloadTime[entry.Value.type])
                    {
                        _preloadRegistry.TryRemove(entry.Key, out _);
                    }
                    else if (DateTimeOffset.UtcNow - entry.Value.lastUpdate >= CacheTime[entry.Value.type])
                    {
                        object value = null;
                        try
                        {
                            switch (entry.Value.type)
                            {
                                case CacheEntryType.ProjectList:
                                    LoadAllProjects();
                                    break;
                                case CacheEntryType.Project:
                                    value = _octopus.GetRepository("Preload Project", entry.Key).Projects.Get((string) entry.Value.state);
                                    break;
                                case CacheEntryType.JsonDocument:
                                    using (var sourceStream = _octopus.GetClient("Preload Json Document", entry.Key).GetContent((string) entry.Value.state))
                                    {
                                        value = sourceStream.ReadAllBytes();
                                    }
                                    break;
                                case CacheEntryType.Release:
                                    var castState = ((ProjectResource project, string version)) entry.Value.state;
                                    value = _octopus.GetRepository("Preload Release", entry.Key).Projects.GetReleaseByVersion(castState.project, castState.version);
                                    break;
                                case CacheEntryType.Channel:
                                    value = _octopus.GetRepository("Preload Channel", entry.Key).Channels.Get((string) entry.Value.state);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.Exception(e);
                        }
                        if (value != null)
                        {
                            _cache.Set(entry.Key, value, CacheTime[entry.Value.type].Add(TimeSpan.FromHours(1)));
                            _preloadRegistry[entry.Key] = (lastAccess: entry.Value.lastAccess, lastUpdate: DateTimeOffset.UtcNow, type: entry.Value.type, state: entry.Value.state);
                        }
                        else if (CachePreloadTime[entry.Value.type] != TimeSpan.MaxValue)
                        {
                            _preloadRegistry.TryRemove(entry.Key, out _);
                        }
                    }
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

        private void LoadAllProjects()
        {
            var projectList = _octopus.GetRepository("Get All Projects", _octopus.BaseUri).Projects.GetAll().ToArray();

            var updatedProjectToken = new CancellationTokenSource();
            _cache.Set(CacheKey(CacheEntryType.ProjectList), projectList, new CancellationChangeToken(updatedProjectToken.Token));
            foreach (var project in projectList)
                _cache.Set(CacheKey(CacheEntryType.Project, project.Name), project, new CancellationChangeToken(updatedProjectToken.Token));

            _projectEvictionTokenSource.Cancel();
            _projectEvictionTokenSource = updatedProjectToken;
        }
    }
}