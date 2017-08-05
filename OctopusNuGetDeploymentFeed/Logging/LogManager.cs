using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace OctopusDeployNuGetFeed.Logging
{
    public class LogManager : ILogger
    {
        private LogManager()
        {
        }

        public ILogger ConsoleLogger { get; } = new ConsoleLogger();
        public ILogger FileLogger { get; } = new FileLogger();

        public static LogManager Current { get; } = new LogManager();

        public void Error(string message)
        {
            ConsoleLogger.Error(message);
            FileLogger.Error(message);
        }

        public void Warning(string message)
        {
            ConsoleLogger.Warning(message);
            FileLogger.Warning(message);
        }

        public void Info(string message)
        {
            ConsoleLogger.Info(message);
            FileLogger.Info(message);
        }

        public void Debug(string message)
        {
#if DEBUG
            ConsoleLogger.Debug(message);
            FileLogger.Debug(message);
#endif
        }

        public void Exception(Exception exception, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            var message = $"{callerTypeName}.{callerMemberName}: {exception.GetType().Name} {exception.Message}. {exception.InnerException?.GetType().Name} {exception.InnerException?.Message}\n{exception.StackTrace}";
            Error(message);
            if (Debugger.IsAttached)
                Debugger.Break();
        }

        public void UnhandledException(Exception exception)
        {
            var message = $"Unhandled Exception: {exception.GetType().Name} {exception.Message}. {exception.InnerException?.GetType().Name} {exception.InnerException?.Message}\n{exception.StackTrace}";
            Error(message);
            if (Debugger.IsAttached)
                Debugger.Break();
        }
    }
}