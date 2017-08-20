using System;
using System.Web.Http;
using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Controllers
{
    [Authorize(Roles = "Authenticated")]
    public class AdminController : ApiController
    {
        private readonly IPackageRepositoryFactory _packageRepositoryFactory;

        public AdminController(IPackageRepositoryFactory packageRepositoryFactory)
        {
            _packageRepositoryFactory = packageRepositoryFactory;
        }

        [HttpGet]
        public IHttpActionResult RegisterFeed()
        {
            var result = _packageRepositoryFactory.GetServer(User).RegisterNuGetFeed(Request.RequestUri.Host);
            return Ok(new
            {
                action = result.created ? "Created" : "Updated",
                result.id
            });
        }

        [HttpGet]
        public IHttpActionResult Cache()
        {
            var cache = _packageRepositoryFactory.GetCache(User);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);

            return Ok(new
            {
                count = cache.Count,
                cacheMemory = cache.ApproximateSize,
                totalMemory = GC.GetTotalMemory(false)
            });
        }
    }
}