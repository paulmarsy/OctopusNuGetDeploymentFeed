using System;
using System.Collections.Generic;
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

        public void TrackDependency(string dependencyName, string commandName, DateTimeOffset startTime, TimeSpan duration, bool success)
        {
        }

        public void TrackException(Exception exception, IDictionary<string, string> properties = null)
        {
        }
    }
}