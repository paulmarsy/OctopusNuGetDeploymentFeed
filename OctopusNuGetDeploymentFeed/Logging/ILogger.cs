using System.Diagnostics;

namespace OctopusDeployNuGetFeed.Logging
{
    public interface ILogger
    {
        void Error(string message);
        void Warning(string message);
        void Info(string message);
        void Debug(string message);
    }
}