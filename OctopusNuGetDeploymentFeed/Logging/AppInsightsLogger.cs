using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ApplicationInsights.DataContracts;

namespace OctopusDeployNuGetFeed.Logging
{
    public class AppInsightsLogger : ILogger
    {
        private readonly IAppInsights _appInsights;

        public AppInsightsLogger(IAppInsights appInsights)
        {
            _appInsights = appInsights;
        }

        public void Critical(string message)
        {
            _appInsights.TrackTrace(message, SeverityLevel.Critical);
        }

        public void Error(string message)
        {
            _appInsights.TrackTrace(message, SeverityLevel.Error);
        }

        public void Warning(string message)
        {
            _appInsights.TrackTrace(message, SeverityLevel.Warning);
        }

        public void Verbose(string message)
        {
        }

        public void Info(string message)
        {
            _appInsights.TrackTrace(message, SeverityLevel.Information);
        }

        public void Exception(Exception exception, string callerFilePath = null, string callerMemberName = null)
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);

            _appInsights.TrackException(exception, new Dictionary<string, string>
            {
                {"UnhandledException", "False"},
                {"CallerFilePath", callerFilePath},
                {"CallerMemberName", callerMemberName},
                {"CallerTypeName", callerTypeName}
            });
        }

        public void UnhandledException(Exception exception)
        {
            _appInsights.TrackException(exception, new Dictionary<string, string>
            {
                {"UnhandledException", "True"}
            });
        }
    }
}