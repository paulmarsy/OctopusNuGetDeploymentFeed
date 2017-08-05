using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace OctopusDeployNuGetFeed.Logging
{
    public class LogManager : ILogger
    {
        private readonly TelemetryClient _telemetryClient;

        private LogManager()
        {
            if (!string.IsNullOrWhiteSpace(Program.AppInsightsKey))
                _telemetryClient = new TelemetryClient { InstrumentationKey = Program.AppInsightsKey };

            ConsoleLogger = new ConsoleLogger();
            FileLogger = new FileLogger();
            FileLogger.Init();
        }

        public ConsoleLogger ConsoleLogger { get; }
        public FileLogger FileLogger { get; }

        public static LogManager Current { get; } = new LogManager();

        public void Error(string message)
        {
            ConsoleLogger.Error(message);
            _telemetryClient?.TrackTrace(message, SeverityLevel.Error);
            FileLogger.Error(message);
        }

        public void Warning(string message)
        {
            ConsoleLogger.Warning(message);
            _telemetryClient?.TrackTrace(message, SeverityLevel.Warning);
            FileLogger.Warning(message);
        }

        public void Info(string message)
        {
            ConsoleLogger.Info(message);
            _telemetryClient?.TrackTrace(message, SeverityLevel.Information);
            FileLogger.Info(message);
        }

        public void Exception(Exception exception, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            ExceptionImpl(exception, $"{callerTypeName}.{callerMemberName}");
        }

        public void UnhandledException(Exception exception)
        {
            ExceptionImpl(exception, "Unhandled Exception");
        }

        private void ExceptionImpl(Exception exception, string source)
        {
            _telemetryClient?.TrackException(exception, new Dictionary<string, string>
            {
                {"Source", source}
            });

#if DEBUG
            if (!Debugger.IsAttached)
                Debugger.Launch();
#endif

            var message = $"{source}: {exception.GetType().Name} {exception.Message}. {exception.InnerException?.GetType().Name} {exception.InnerException?.Message}\n{exception.StackTrace}";
            Error(message);
            if (Debugger.IsAttached)
                Debugger.Break();
        }
    }
}