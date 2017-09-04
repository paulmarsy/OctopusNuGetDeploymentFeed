using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using Microsoft.ApplicationInsights.DataContracts;

namespace OctopusDeployNuGetFeed.Logging
{
    public interface IAppInsights : ILogger
    {
        bool IsEnabled { get; }
        void Initialize();
        void TrackTrace(string message, SeverityLevel severityLevel);
        void TrackDependency(string dependencyTypeName, string target, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, string resultCode, bool success);
        void TrackEvent(string eventName, IDictionary<string, string> properties = null);

        void TrackException(Exception exception, IDictionary<string, string> properties = null);
        void TrackHealth(string healthMessage, HealthState state);
        void TrackMetric(string name, double value);
        void TrackMetric(string name, int count, double sum, double min, double max, double standardDeviation);
        void SetCloudContext(ServiceContext context);
    }
}