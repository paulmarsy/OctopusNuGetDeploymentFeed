using System.Threading.Tasks;
using System.Web.Http;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.Services.ControlService;

namespace OctopusDeployNuGetFeed.Controllers
{
    [Authorize(Roles = "Authenticated")]
    public class AdminController : ApiController
    {
        private readonly IOctopusClientFactory _octopusClientFactory;
        private readonly IServiceControl _serviceControl;

        public AdminController(IOctopusClientFactory octopusClientFactory, IServiceControl serviceControl)
        {
            _octopusClientFactory = octopusClientFactory;
            _serviceControl = serviceControl;
        }

        [HttpGet]
        public async Task<IHttpActionResult> RegisterFeed()
        {
            var result = await _octopusClientFactory.GetConnection(OctopusCredential.FromPrincipal(User)).RegisterNuGetFeed(Request.RequestUri.Host);
            return Ok(new
            {
                action = result.created ? "Created" : "Updated",
                result.id
            });
        }

        [HttpGet]
        public async Task<IHttpActionResult> Decache()
        {
            await _serviceControl.Decache();
            return Ok();
        }
    }
}