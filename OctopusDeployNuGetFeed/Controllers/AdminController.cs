using System;
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
        public IHttpActionResult Stats()
        {
            var cache = _octopusClientFactory.GetServer(OctopusCredential.FromPrincipal(User));

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);

            return Ok(new
            {
                Instances = _octopusClientFactory.RegisteredOctopusServers,
                CacheEntries = cache.CachedItems,
                CachePreloadEntries = cache.PreloadCount,
                CacheMemory = cache.CacheSize,
                TotalMemory = GC.GetTotalMemory(false)
            });
        }

        [HttpGet]
        public async Task<IHttpActionResult> Decache()
        {
            await _serviceControl.Decache();
            return Stats();
        }
    }
}