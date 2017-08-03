using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.Logging;
using SemanticVersion = NuGet.SemanticVersion;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusProjectCache : BaseOctopusRepository
    {
        private readonly object _allProjectSyncLock = new object();
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;


        public OctopusProjectCache(string baseUri, string apiKey, ILogger logger) : base(baseUri, apiKey)
        {
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                CompactOnMemoryPressure = true,
                ExpirationScanFrequency = TimeSpan.FromMinutes(10)
            });
            _logger = logger;
        }


        public ProjectResource GetProject(string name)
        {
            if (_cache.TryGetValue(name, out ProjectResource project))
                return project;

            return GetAllProjects().SingleOrDefault(currentProject => string.Equals(currentProject.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<ProjectResource> GetAllProjects()
        {
            const string projectListCacheKey = "Projects-All";
            if (!_cache.TryGetValue(projectListCacheKey, out IEnumerable<ProjectResource> projects))
                lock (_allProjectSyncLock)
                {
                    projects = _cache.GetOrCreate(projectListCacheKey, entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                        return Client.Repository.Projects.GetAll().GetAwaiter().GetResult();
                    });
                }
            foreach (var project in projects)
                yield return _cache.Set(project.Name, project, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromHours(1)
                });
        }

        public ChannelResource GetChannel(string channelId)
        {
            return _cache.GetOrCreate(channelId, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(2);
                return Client.Repository.Channels.Get(channelId).GetAwaiter().GetResult();
            });
        }

        public IEnumerable<ReleaseResource> ListReleases(ProjectResource project)
        {
            foreach (var release in Client.Repository.Projects.GetReleases(project).GetAwaiter().GetResult().Items)
            {
                var cacheKey = GetReleaseCacheKey(project, release.Version);
                if (cacheKey == null)
                    continue;

                yield return _cache.Set(cacheKey, release, TimeSpan.FromHours(1));
            }
        }

        private string GetReleaseCacheKey(ProjectResource project, string version)
        {
            if (!SemanticVersion.TryParse(version, out SemanticVersion semver))
            {
                _logger.Warning($"GetReleaseCacheKey.SemanticVersion.TryParse: {project.Name} ({project.Id}) {version}");
                return null;
            }
            return $"{project.Id}-{semver.ToNormalizedString()}";
        }

        public ReleaseResource GetRelease(ProjectResource project, string version)
        {
            if (_cache.TryGetValue(GetReleaseCacheKey(project, version), out ReleaseResource release))
                return release;

            return ListReleases(project).SingleOrDefault(package =>
            {
                var semver = SemanticVersion.Parse(version);
                return string.Equals(version, semver.ToOriginalString(), StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(version, semver.ToNormalizedString(), StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(version, semver.ToFullString(), StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}