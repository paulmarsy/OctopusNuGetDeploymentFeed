using System.Runtime.ExceptionServices;
using System.Web.Http.ExceptionHandling;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public class PassthroughExceptionHandler : ExceptionHandler
    {
        private readonly LogManager _logManager;

        public PassthroughExceptionHandler(LogManager logManager)
        {
            _logManager = logManager;
        }

        public override void Handle(ExceptionHandlerContext context)
        {
            _logManager.Exception(context.Exception);
            ExceptionDispatchInfo.Capture(context.Exception).Throw();
        }
    }
}