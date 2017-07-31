namespace OctopusDeployNuGetFeed.Logging
{
    public class LogManager : ILogger
    {
        private readonly ILogger _console = new ConsoleLogger();
        private readonly ILogger _logFile = new FileLogger();
        public void Error(string message)
        {
            _console.Error(message);
            _logFile.Error(message);
        }

        public void Warning(string message)
        {
            _console.Warning(message);
            _logFile.Warning(message);
        }

        public void Info(string message)
        {
            _console.Info(message);
            _logFile.Info(message);
        }

        public void Debug(string message)
        {
            _console.Debug(message);
            _logFile.Debug(message);
        }
    }
}