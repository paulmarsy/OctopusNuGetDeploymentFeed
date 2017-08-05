using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.DataContracts;

namespace OctopusDeployNuGetFeed.Logging
{
    public class AppInsightsLogger : ILogger
    {
        public void Critical(string message)
        {
            Startup.AppInsights.TelemetryClient?.TrackTrace(message, SeverityLevel.Critical);
        }

        public void Error(string message)
        {
            Startup.AppInsights.TelemetryClient?.TrackTrace(message, SeverityLevel.Error);
        }

        public void Warning(string message)
        {
            Startup.AppInsights.TelemetryClient?.TrackTrace(message, SeverityLevel.Warning);
        }

        public void Info(string message)
        {
            Startup.AppInsights.TelemetryClient?.TrackTrace(message, SeverityLevel.Information);
        }

        public void Exception(Exception exception, string source)
        {
            Startup.AppInsights.TelemetryClient?.TrackException(exception, new Dictionary<string, string>
            {
                {"Source", source}
            });
        }
    }
}