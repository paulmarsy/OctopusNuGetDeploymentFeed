using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public class GlobalExceptionMiddleware : OwinMiddleware
    {
        private readonly ILogger _logger = Startup.Logger;

        public GlobalExceptionMiddleware(OwinMiddleware next) : base(next)
        { }

        public override async Task Invoke(IOwinContext context)
        {
            try
            {
                await Next.Invoke(context);
            }
            catch (Exception e)
            {
                _logger.Error($"{context.Request.RemoteIpAddress} {context.Request.Method} {context.Request.Uri}\n{e.Message}. {e.InnerException?.Message}\n{e.StackTrace}");
            }
        }
    }
}
