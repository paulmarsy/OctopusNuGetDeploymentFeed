using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using Microsoft.ApplicationInsights.DataContracts;

namespace OctopusDeployNuGetFeed.Logging
{
    public class AppInsightsNotConfigured : IAppInsights
    {
        public bool IsEnabled => false;

        public void Initialize()
        {
        }

        public void TrackTrace(string message, SeverityLevel severityLevel)
        {
        }

        public void TrackDependency(string dependencyTypeName, string target, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, string resultCode, bool success)
        {
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties = null)
        {
        }

        public void TrackException(Exception exception, IDictionary<string, string> properties = null)
        {
        }

        public void TrackHealth(string healthMessage, HealthState state)
        {
        }

        public void TrackMetric(string name, double value)
        {
        }

        public void TrackMetric(string name, int count, double sum, double min, double max, double standardDeviation)
        {
        }

        public void SetCloudContext(ServiceContext context)
        {
        }

        public void Exception(Exception exception, string callerFilePath = null, string callerMemberName = null)
        {
        }

        public void UnhandledException(Exception exception)
        {
        }

        public void Critical(string message)
        {
        }

        public void Error(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Verbose(string message)
        {
        }

        public void Info(string message)
        {
        }
    }
}