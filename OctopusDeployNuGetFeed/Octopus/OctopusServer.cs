using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        public bool ProjectExists(string projectName)
        {
            return _cache.TryGetValue(CacheKey(CacheEntryType.Project, projectName), out ProjectResource _);
        }

        public int CachedItems => _cache.Count;
        public long CacheSize => _cacheSizeRef.ApproximateSize;

        public async Task<ReleaseResource> GetLatestReleaseAsync(ProjectResource project)
        {
            SemanticVersion semver = null;
            ReleaseResource release = null;
            for (var skip = 0; semver == null; skip++)
            {
                release = (await _octopus.GetClient("Get Latest Release", project.Name).List<ReleaseResource>(project.Link("Releases"), new {skip = skip, take = 1})).Items.FirstOrDefault();
                if (release == null)
                    return null;
                semver = release.Version.ToSemanticVersion();
            }

            RegisterPreloadAccess(CacheEntryType.Release, true, (project, release.Version), project.Id, semver.ToNormalizedString());
            return _cache.Set(CacheKey(CacheEntryType.Release, project.Id, semver.ToNormalizedString()), release, CacheTime[CacheEntryType.Release]);
        }

        public async Task<string> GetJsonAsync(Resource resource)
        {
            var jsonUrl = resource.Link("Self");
            RegisterPreloadAccess(CacheEntryType.JsonDocument, false, jsonUrl, jsonUrl);

            return await _cache.GetOrCreateAsync(CacheKey(CacheEntryType.JsonDocument, jsonUrl), async entry =>
            {
                RegisterPreloadAccess(CacheEntryType.JsonDocument, true, jsonUrl, jsonUrl);
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.JsonDocument]);
                using (var sourceStream = await _octopus.GetClient("Get Json Document", jsonUrl).GetContent(jsonUrl))
                using (var streamReader = new StreamReader(sourceStream, Encoding.UTF8, false))
                {
                    return streamReader.ReadToEnd();
                }
            });
        }

        public async System.Threading.Tasks.Task<ProjectResource> GetProjectAsync(string name)
        {
            return await _cache.GetOrCreateAsync(CacheKey(CacheEntryType.Project, name), async entry =>
            {
                entry.AddExpirationToken(new CancellationChangeToken(_projectEvictionTokenSource.Token));
                return await _octopus.GetRepository("Find Project", name).Projects.FindByName(name);
            });
        }

        public IEnumerable<ProjectResource> GetAllProjects()
        {
            if (!_cache.TryGetValue(CacheKey(CacheEntryType.ProjectList), out IEnumerable<ProjectResource> projects))
                return Enumerable.Empty<ProjectResource>();

            return projects;
        }

        public async Task<ChannelResource> GetChannelAsync(string channelId)
        {
            RegisterPreloadAccess(CacheEntryType.Channel, false, channelId, channelId);

            return await _cache.GetOrCreateAsync(CacheKey(CacheEntryType.Channel, channelId), async entry =>
            {
                RegisterPreloadAccess(CacheEntryType.Channel, true, channelId, channelId);
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.Channel]);
                return await _octopus.GetRepository("Get Channel", channelId).Channels.Get(channelId);
            });
        }

        public async Task<IList<ReleaseResource>> GetAllReleasesAsync(ProjectResource project)
        {
            var releases = await _cache.GetOrCreateAsync(CacheKey(CacheEntryType.ReleaseList, project.Id), async entry =>
            {
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.ReleaseList]); // Cache for pagination calls

                return await _octopus.GetRepository("Get All Releases", project.Name).Projects.GetAllReleases(project);
            });
            return UpdateReleaseCache(project, releases).ToList();
        }

        private IEnumerable<ReleaseResource> UpdateReleaseCache(ProjectResource project, IEnumerable<ReleaseResource> releases)
        {
            foreach (var release in releases)
            {
                var semver = release.Version.ToSemanticVersion();
                if (semver == null)
                    continue;

                yield return _cache.Set(CacheKey(CacheEntryType.Release, project.Id, semver.ToNormalizedString()), release, CacheTime[CacheEntryType.Release]);
            }
        }

        public async Task<ReleaseResource> GetReleaseAsync(ProjectResource project, SemanticVersion semver)
        {
            var updated = false;
            if (!_cache.TryGetValue(CacheKey(CacheEntryType.Release, project.Id, semver.ToNormalizedString()), out ReleaseResource release))
            {
                // Try for an exact match, failing that go for a normalized match
                var allReleases = await GetAllReleasesAsync(project);
                release = allReleases.SingleOrDefault(package => string.Equals(semver.ToOriginalString(), package.Version, StringComparison.OrdinalIgnoreCase)) ??
                          allReleases.FirstOrDefault(package => string.Equals(semver.ToNormalizedString(), package.Version.ToSemanticVersion().ToNormalizedString(), StringComparison.OrdinalIgnoreCase));
                updated = true;
            }
            if (release == null)
                return null;

            RegisterPreloadAccess(CacheEntryType.Release, updated, (project, release.Version), project.Id, semver.ToNormalizedString());
            return release;
        }

        public Task<byte[]> GetNuGetPackageAsync(ProjectResource project, ReleaseResource release, Func<Task<byte[]>> nugetPackageFactory)
        {
            return _cache.GetOrCreateAsync(CacheKey(CacheEntryType.NuGetPackage, project.Id, release.Id), async entry =>
            {
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.NuGetPackage]);
                return await nugetPackageFactory(); 
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
                ExecutePreloadScanAsync().GetAwaiter().GetResult();
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

        private async Task ExecutePreloadScanAsync()
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
                        value = await GetPreloadObject(entry.Key, entry.Value.type, entry.Value.state);
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

        private async Task<object> GetPreloadObject(string key, CacheEntryType type, object state)
        {
            switch (type)
            {
                case CacheEntryType.ProjectList:
                    var projectList = await _octopus.GetRepository("Get All Projects", _octopus.BaseUri).Projects.GetAll();

                    var updatedProjectToken = new CancellationTokenSource();
                    _cache.Set(CacheKey(CacheEntryType.ProjectList), projectList, new CancellationChangeToken(updatedProjectToken.Token));
                    foreach (var project in projectList)
                        _cache.Set(CacheKey(CacheEntryType.Project, project.Name), project, new CancellationChangeToken(updatedProjectToken.Token));

                    _projectEvictionTokenSource.Cancel();
                    _projectEvictionTokenSource = updatedProjectToken;
                    return null;
                case CacheEntryType.Project:
                    return await _octopus.GetRepository("Preload Project", key).Projects.Get((string)state);
                case CacheEntryType.JsonDocument:
                    using (var sourceStream = await _octopus.GetClient("Preload Json Document", key).GetContent((string) state))
                    {
                        return sourceStream.ReadToEnd();
                    }
                case CacheEntryType.Release:
                    var castState = ((ProjectResource project, string version))state;
                    return await  _octopus.GetRepository("Preload Release", key).Projects.GetReleaseByVersion(castState.project, castState.version);
                case CacheEntryType.Channel:
                   return await _octopus.GetRepository("Preload Channel", key).Channels.Get((string) state);
                default:
                    throw new ArgumentOutOfRangeException();
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
    }
}