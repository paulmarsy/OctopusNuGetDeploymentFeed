using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
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
                Content = new StringContent($"404: {uri}\nNuGet Feed Endpoint: {Startup.BaseAddress}nuget", Encoding.UTF8, "text/plain"),
                RequestMessage = Request
            });
        }
    }
}