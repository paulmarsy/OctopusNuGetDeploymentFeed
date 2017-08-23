﻿using System;
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
using OctopusDeployNuGetFeed.Infrastructure;
using ILogger = OctopusDeployNuGetFeed.Logging.ILogger;
using MemoryCache = Microsoft.Extensions.Caching.Memory.MemoryCache;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    [SuppressMessage("ReSharper", "RedundantAnonymousTypePropertyName", Justification = "Explicitly defined to match REST parameter name")]
    public class OctopusCache : IOctopusCache, IDisposable
    {
        private static readonly IReadOnlyDictionary<CacheEntryType, TimeSpan> CachePreloadTime = new Dictionary<CacheEntryType, TimeSpan>
        {
            {CacheEntryType.ProjectList, TimeSpan.MaxValue},
            {CacheEntryType.Channel, TimeSpan.FromDays(5)},
            {CacheEntryType.Release, TimeSpan.FromDays(3)},
            {CacheEntryType.JsonDocument, TimeSpan.FromDays(3)}
        };

        private static readonly IReadOnlyDictionary<CacheEntryType, TimeSpan> CacheTime = new Dictionary<CacheEntryType, TimeSpan>
        {
            {CacheEntryType.ProjectList, TimeSpan.FromHours(2)},
            {CacheEntryType.Channel, TimeSpan.FromDays(3)},
            {CacheEntryType.Release, TimeSpan.FromDays(2)},
            {CacheEntryType.JsonDocument, TimeSpan.FromDays(2)},
            {CacheEntryType.ReleaseList, TimeSpan.FromMinutes(3)}, // Cache for pagination calls
            {CacheEntryType.NuGetPackage, TimeSpan.FromHours(1)}
        };

        private readonly MemoryCache _cache;
        private readonly SizedReference _cacheSizeRef;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, (DateTimeOffset lastAccess, DateTimeOffset lastUpdate, CacheEntryType type, object state)> _preloadRegistry = new ConcurrentDictionary<string, (DateTimeOffset lastAccess, DateTimeOffset lastUpdate, CacheEntryType type, object state)>();
        private readonly OctopusServer _server;
        private readonly Timer _timer;
        private CancellationTokenSource _projectEvictionTokenSource;

        public OctopusCache(OctopusServer server, ILogger logger)
        {
            _server = server;
            _logger = logger;
            _cache = new MemoryCache(new MemoryCacheOptions());
            _cacheSizeRef = new SizedReference(_cache);
            _projectEvictionTokenSource = new CancellationTokenSource();
            RegisterPreloadAccess(CacheEntryType.ProjectList, false, null);
            _timer = new Timer(TimerHandler, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        public int Preloads { get; private set; }

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

        public int Count => _cache.Count;
        public long ApproximateSize => _cacheSizeRef.ApproximateSize;

        public ReleaseResource GetLatestRelease(ProjectResource project)
        {
            SemanticVersion semver = null;
            ReleaseResource release = null;
            for (var skip = 0; semver == null; skip++)
            {
                release = _server.GetClient("Get Latest Release", project.Name).List<ReleaseResource>(project.Link("Releases"), new {skip = skip, take = 1}).Items.FirstOrDefault();
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
                using (var sourceStream = _server.GetClient("Get Json Document", jsonUrl).GetContent(jsonUrl))
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
                return _server.GetRepository("Find Project", name).Projects.FindByName(name);
            });
        }

        public IEnumerable<ProjectResource> GetAllProjects()
        {
            var currentProjectSet = _projectEvictionTokenSource;

            if (!_cache.TryGetValue(CacheKey(CacheEntryType.ProjectList), out IEnumerable<ProjectResource> projects))
                projects = LoadAllProjects(currentProjectSet);

            return projects;
        }

        public ChannelResource GetChannel(string channelId)
        {
            RegisterPreloadAccess(CacheEntryType.Channel, false, channelId, channelId);

            return _cache.GetOrCreate(CacheKey(CacheEntryType.Channel, channelId), entry =>
            {
                RegisterPreloadAccess(CacheEntryType.Channel, true, channelId, channelId);
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.Channel]);
                return _server.GetRepository("Get Channel", channelId).Channels.Get(channelId);
            });
        }

        public IEnumerable<ReleaseResource> ListReleases(ProjectResource project)
        {
            foreach (var release in _cache.GetOrCreate(CacheKey(CacheEntryType.ReleaseList, project.Id), entry =>
            {
                entry.SetAbsoluteExpiration(CacheTime[CacheEntryType.ReleaseList]); // Cache for pagination calls

                return _server.GetRepository("Get All Releases", project.Name).Projects.GetAllReleases(project);
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
                        var options = new MemoryCacheEntryOptions();
                        options.SetAbsoluteExpiration(CacheTime[entry.Value.type].Add(TimeSpan.FromHours(1)));
                        try
                        {
                            switch (entry.Value.type)
                            {
                                case CacheEntryType.ProjectList:
                                    value = LoadAllProjects(_projectEvictionTokenSource);
                                    options.AddExpirationToken(new CancellationChangeToken(_projectEvictionTokenSource.Token));
                                    break;
                                case CacheEntryType.JsonDocument:
                                    using (var sourceStream = _server.GetClient("Preload Json Document", entry.Key).GetContent((string) entry.Value.state))
                                    {
                                        value = sourceStream.ReadAllBytes();
                                    }
                                    break;
                                case CacheEntryType.Release:
                                    var castState = ((ProjectResource prpject, string version)) entry.Value.state;
                                    value = _server.GetRepository("Preload Release", entry.Key).Projects.GetReleaseByVersion(castState.prpject, castState.version);
                                    break;
                                case CacheEntryType.Channel:
                                    value = _server.GetRepository("Preload Channel", entry.Key).Channels.Get((string) entry.Value.state);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.Exception(e);
                        }
                        if (value == null && CachePreloadTime[entry.Value.type] != TimeSpan.MaxValue)
                        {
                            _preloadRegistry.TryRemove(entry.Key, out _);
                        }
                        else
                        {
                            _cache.Set(entry.Key, value, options);
                            _preloadRegistry[entry.Key] = (lastAccess: entry.Value.lastAccess, lastUpdate: DateTimeOffset.UtcNow, type: entry.Value.type, state: entry.Value.state);
                            Preloads++;
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

        private IEnumerable<ProjectResource> LoadAllProjects(CancellationTokenSource currentSet)
        {
            lock (currentSet)
            {
                if (currentSet.IsCancellationRequested || currentSet != _projectEvictionTokenSource)
                    return _cache.Get<IEnumerable<ProjectResource>>(CacheKey(CacheEntryType.ProjectList));

                var projectEvictionTokenSource = new CancellationTokenSource();
                var projectList = _server.GetRepository("Get All Projects", _server.BaseUri).Projects.GetAll().ToArray();

                currentSet.Cancel();

                var options = new MemoryCacheEntryOptions();
                options.SetAbsoluteExpiration(CacheTime[CacheEntryType.Project]);
                options.AddExpirationToken(new CancellationChangeToken(projectEvictionTokenSource.Token));
                _cache.Set(CacheKey(CacheEntryType.ProjectList), projectList, options);
                foreach (var project in projectList)
                    _cache.Set(CacheKey(CacheEntryType.Project, project.Name), project, options);

                _projectEvictionTokenSource = projectEvictionTokenSource;

                return projectList;
            }
        }
    }
}