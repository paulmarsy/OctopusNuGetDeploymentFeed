using System.Runtime.ExceptionServices;
using System.Web.Http.ExceptionHandling;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public class PassthroughExceptionHandler : ExceptionHandler
    {
        public override void Handle(ExceptionHandlerContext context)
        {
            ExceptionDispatchInfo.Capture(context.Exception).Throw();
        }
    }
}