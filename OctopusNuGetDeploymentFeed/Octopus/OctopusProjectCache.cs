using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Octopus.Client.Model;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Octopus.ProjectCache
{
    public class OctopusProjectCache : BaseOctopusRepository, IDisposable
    {
        private readonly ILogger _logger;
        private readonly Timer _timer;
        private IReadOnlyCollection<ProjectResource> _projects;

        public OctopusProjectCache(string baseUri, string apiKey, ILogger logger) : base(baseUri, apiKey)
        {
            _logger = logger;
            UpdateCache();
            _timer = new Timer(TimerHandler, null, MilliSecondsLeftTilTheHour(), Timeout.Infinite);
        }

        public IEnumerable<ProjectResource> Projects => _projects;

        public void Dispose()
        {
            _timer?.Dispose();
        }

        private void TimerHandler(object state)
        {
            UpdateCache();
            _timer.Change(MilliSecondsLeftTilTheHour(), Timeout.Infinite);
        }

        private void UpdateCache()
        {
            try
            {
                _logger.Info("Refreshing cache...");
                var projects = Client.Repository.Projects.GetAll().GetAwaiter().GetResult().AsReadOnly();
                Interlocked.Exchange(ref _projects, projects);
                _logger.Info("Cache updated");
            }
            catch (Exception e)
            {
                _logger.Error($"OctopusProjectCache.TimerHandler: {e.Message}. {e.InnerException?.Message}\n{e.StackTrace}");
            }
        }

        private static int MilliSecondsLeftTilTheHour()
        {
            var now = DateTime.Now;
            var interval = ((59 - now.Minute) * 60 + 59 - now.Second) * 1000;

            if (interval == 0)
                interval = 60 * 60 * 1000;

            return interval;
        }

        public ProjectResource GetProjectByName(string name)
        {
            return Projects.SingleOrDefault(project => string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<ProjectResource> FindProjects(string searchTerm)
        {
            return Projects.Where(project => project.Name.WildcardMatch($"*{searchTerm}*"));
        }
    }
}