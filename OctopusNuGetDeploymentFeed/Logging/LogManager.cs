namespace OctopusDeployNuGetFeed.Logging
{
    public class LogManager : ILogger
    {
        private LogManager()
        {
        }
        public ILogger ConsoleLogger { get; } = new ConsoleLogger();
        public ILogger FileLogger { get; } = new FileLogger();

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

        public static LogManager Current { get; } = new LogManager();
    }
}