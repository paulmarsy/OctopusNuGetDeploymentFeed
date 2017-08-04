using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed.Controllers
{
    public class DefaultController : ApiController
    {
        private readonly ILogger _logger = Startup.Logger;

        public IHttpActionResult Get(string uri)
        {
            _logger.Info($"DefaultController.Get: {uri}");
            return ResponseMessage(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"404: {uri}\n" +
                                            $"NuGet Feed Endpoint: {Startup.BaseAddress}nuget\n", Encoding.UTF8, "text/plain"),
                RequestMessage = Request
            });
        }

        public IHttpActionResult Index()
        {
            return new PlainTextResult("Octopus - NuGet Deployment Feed\n" +
                                       "by Paul Marston\n" +
                                       "https://github.com/paulmarsy/OctopusNuGetDeploymentFeed", Request);
        }
    }
}