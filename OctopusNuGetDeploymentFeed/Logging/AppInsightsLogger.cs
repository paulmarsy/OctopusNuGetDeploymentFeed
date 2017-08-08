using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.ApplicationInsights.DataContracts;

namespace OctopusDeployNuGetFeed.Logging
{
    public class AppInsightsLogger : ILogger
    {
        private readonly IAppInsights _appInsights;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AppInsightsLogger(IAppInsights appInsights)
        {
            _appInsights = appInsights;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Critical(string message)
        {
            _appInsights.TrackTrace(message, SeverityLevel.Critical);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Error(string message)
        {
            _appInsights.TrackTrace(message, SeverityLevel.Error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Warning(string message)
        {
            _appInsights.TrackTrace(message, SeverityLevel.Warning);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Info(string message)
        {
            _appInsights.TrackTrace(message, SeverityLevel.Information);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnhandledException(Exception exception)
        {
            _appInsights.TrackException(exception, new Dictionary<string, string>
            {
                {"UnhandledException", "True"}
            });
        }
    }
}