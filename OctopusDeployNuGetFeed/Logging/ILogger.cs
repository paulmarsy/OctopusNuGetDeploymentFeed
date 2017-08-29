using System;
using System.Runtime.CompilerServices;

namespace OctopusDeployNuGetFeed.Logging
{
    public interface ILogger
    {
        void Exception(Exception exception, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null);
        void UnhandledException(Exception exception);
        void Critical(string message);
        void Error(string message);
        void Warning(string message);
        void Verbose(string message);
        void Info(string message);
    }
}