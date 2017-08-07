using System.Runtime.ExceptionServices;
using System.Web.Http.ExceptionHandling;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public class PassthroughExceptionHandler : ExceptionHandler
    {
        private readonly ILogger _logger;

        public PassthroughExceptionHandler(ILogger logger)
        {
            _logger = logger;
        }

        public override void Handle(ExceptionHandlerContext context)
        {
            _logger.Exception(context.Exception);
            ExceptionDispatchInfo.Capture(context.Exception).Throw();
        }
    }
}