using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace OctopusDeployNuGetFeed.Logging
{
    public class LogManager : ILogger
    {
        private readonly AppInsightsLogger _appInsightsLogger;
        private readonly ConsoleLogger _consoleLogger;

        public LogManager(IAppInsights appInsights)
        {
            _consoleLogger = new ConsoleLogger();
            _appInsightsLogger = new AppInsightsLogger(appInsights);
        }

        public void Critical(string message)
        {
            _consoleLogger.Critical(message);
            _appInsightsLogger.Critical(message);
        }

        public void Error(string message)
        {
            _consoleLogger.Error(message);
            _appInsightsLogger.Error(message);
        }

        public void Warning(string message)
        {
            _consoleLogger.Warning(message);
            _appInsightsLogger.Warning(message);
        }

        public void Verbose(string message)
        {
            _consoleLogger.Verbose(message);
            _appInsightsLogger.Verbose(message);
        }

        public void Info(string message)
        {
            _consoleLogger.Info(message);
            _appInsightsLogger.Info(message);
        }

        public void Exception(Exception exception, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            _appInsightsLogger.Exception(exception, callerFilePath, callerMemberName);
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            ExceptionImpl(exception, $"{callerTypeName}.{callerMemberName}");
        }

        public void UnhandledException(Exception exception)
        {
            _appInsightsLogger.UnhandledException(exception);
            ExceptionImpl(exception, "Unhandled Exception");
        }

        private void ExceptionImpl(Exception exception, string source)
        {
            _consoleLogger.Critical($"{source}: {exception.GetType().Name} {exception.Message}. {exception.InnerException?.GetType().Name} {exception.InnerException?.Message}\n{exception.StackTrace}");
#if DEBUG
            if (!System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Launch();

            System.Diagnostics.Debugger.Break();
#endif
        }
    }
}