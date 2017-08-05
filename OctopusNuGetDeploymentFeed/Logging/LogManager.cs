using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace OctopusDeployNuGetFeed.Logging
{
    public class LogManager : ILogger
    {
        private LogManager()
        {
            ConsoleLogger = new ConsoleLogger();
            FileLogger = new FileLogger(ConsoleLogger);
            AppInsightsLogger = new AppInsightsLogger();
        }

        public AppInsightsLogger AppInsightsLogger { get; }
        public ConsoleLogger ConsoleLogger { get; }
        public FileLogger FileLogger { get; }

        public static LogManager Current { get; } = new LogManager();

        public void Critical(string message)
        {
            ConsoleLogger.Critical(message);
            AppInsightsLogger.Critical(message);
            FileLogger.Critical(message);
        }

        public void Error(string message)
        {
            ConsoleLogger.Error(message);
            AppInsightsLogger.Error(message);
            FileLogger.Error(message);
        }

        public void Warning(string message)
        {
            ConsoleLogger.Warning(message);
            AppInsightsLogger.Warning(message);
            FileLogger.Warning(message);
        }

        public void Info(string message)
        {
            ConsoleLogger.Info(message);
            AppInsightsLogger.Info(message);
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
            AppInsightsLogger.Exception(exception, source);
            Critical($"{source}: {exception.GetType().Name} {exception.Message}. {exception.InnerException?.GetType().Name} {exception.InnerException?.Message}\n{exception.StackTrace}");
#if DEBUG
            if (!Debugger.IsAttached)
                Debugger.Launch();

              Debugger.Break();
#endif
        }
    }
}