namespace OctopusDeployNuGetFeed.Logging
{
    public interface ILogger
    {
        void Critical(string message);
        void Error(string message);
        void Warning(string message);
        void Info(string message);
    }
}