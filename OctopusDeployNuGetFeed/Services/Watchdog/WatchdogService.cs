using System;
using System.Fabric;
using System.Fabric.Health;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Runtime;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Services.Watchdog
{
    public class WatchdogService : StatelessService
    {
        private readonly IAppInsights _appInsights;

        public WatchdogService(StatelessServiceContext serviceContext, IAppInsights appInsights) : base(serviceContext)
        {
            _appInsights = appInsights;
        }


        private async Task ReportMetricsAsync(FabricClient client)
        {
            var clusterLoadInformation = await client.QueryManager.GetClusterLoadInformationAsync();
            foreach (var metric in clusterLoadInformation.LoadMetricInformationList)
                _appInsights.TrackMetric(metric.Name, 1, metric.ClusterLoad, metric.MinNodeLoadValue, metric.MaxNodeLoadValue, metric.DeviationAfter);
        }

        private async Task ReportClusterHealthAsync(FabricClient client)
        {
            var health = await client.HealthManager.GetClusterHealthAsync();

            _appInsights.TrackHealth(health.ToString(), health.AggregatedHealthState);

            foreach (var appHealth in health.ApplicationHealthStates.Where(app => app.AggregatedHealthState != HealthState.Ok))
                _appInsights.TrackHealth(appHealth.ToString(), appHealth.AggregatedHealthState);

            foreach (var nodeHealth in health.NodeHealthStates.Where(node => node.AggregatedHealthState != HealthState.Ok))
                _appInsights.TrackHealth(nodeHealth.ToString(), nodeHealth.AggregatedHealthState);
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            _appInsights.SetCloudContext(Context);
            var client = new FabricClient();

            while (!cancellationToken.IsCancellationRequested)
            {
                await ReportMetricsAsync(client);
                await ReportClusterHealthAsync(client);

                await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);
            }
        }
    }
}