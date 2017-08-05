using System;
using System.Web.Http;
using System.Web.Http.Results;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Controllers
{
    public class DefaultController : ApiController
    {
        private readonly ILogger _logger = Startup.Logger;

        public IHttpActionResult Get(string uri)
        {
            _logger.Info($"DefaultController.404: {uri}");
            return new RedirectResult(new Uri("/", UriKind.Relative), Request);
        }

        [HttpGet]
        public IHttpActionResult Index()
        {
            return new PlainTextResult("Octopus - NuGet Deployment Feed\n" +
                                       "by Paul Marston\n" +
                                       "https://github.com/paulmarsy/OctopusNuGetDeploymentFeed", Request);
        }
    }
}