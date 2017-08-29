using System.Web.Http.ExceptionHandling;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.OWIN
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
        }
    }
}