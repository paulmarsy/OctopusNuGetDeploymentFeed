using System;
using System.Runtime.CompilerServices;

namespace OctopusDeployNuGetFeed.Logging
{
    public class LogManager : ILogger
    {
        private readonly IAppInsights _appInsightsLogger;
        private readonly ConsoleLogger _consoleLogger;
        private readonly ServiceFabricEventSource _serviceFabricLogger;

        public LogManager(IAppInsights appInsights, ServiceFabricEventSource serviceFabricEventSource)
        {
            _consoleLogger = new ConsoleLogger();
            _appInsightsLogger = appInsights;
            _serviceFabricLogger = serviceFabricEventSource;
        }

        public void Critical(string message)
        {
            DispatchLogEvent(logger => logger.Critical(message));
        }

        public void Error(string message)
        {
            DispatchLogEvent(logger => logger.Error(message));
        }

        public void Warning(string message)
        {
            DispatchLogEvent(logger => logger.Warning(message));
        }

        public void Verbose(string message)
        {
            DispatchLogEvent(logger => logger.Verbose(message));
        }

        public void Info(string message)
        {
            DispatchLogEvent(logger => logger.Info(message));
        }

        public void Exception(Exception exception, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            DispatchLogEvent(logger => logger.Exception(exception, callerFilePath, callerMemberName));
        }

        public void UnhandledException(Exception exception)
        {
            DispatchLogEvent(logger => logger.UnhandledException(exception));
        }

        private void DispatchLogEvent(Action<ILogger> logEvent)
        {
            logEvent(_consoleLogger);
            logEvent(_appInsightsLogger);
            logEvent(_serviceFabricLogger);
        }
    }
}