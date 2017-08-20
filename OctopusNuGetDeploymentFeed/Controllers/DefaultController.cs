using System;
using System.Web.Http;
using System.Web.Http.Results;
using OctopusDeployNuGetFeed.Infrastructure;

namespace OctopusDeployNuGetFeed.Controllers
{
    public class DefaultController : ApiController
    {
        public IHttpActionResult Get(string uri)
        {
            return new RedirectResult(new Uri("/", UriKind.Relative), Request);
        }

        [HttpGet]
        public IHttpActionResult Index()
        {
            return new PlainTextResult("Octopus Deploy - NuGet Deployment Feed\n" +
                                       $"v{Program.Version}\n" +
                                       "by Paul Marston\n" +
                                       "https://github.com/paulmarsy/OctopusNuGetDeploymentFeed", Request);
        }
    }
}