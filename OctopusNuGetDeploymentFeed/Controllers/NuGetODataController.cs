using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.OData;

namespace OctopusDeployNuGetFeed.Controllers
{
    [NuGetODataControllerConfiguration]
    [Authorize]
    public class NuGetODataController : ODataController
    {
        private const int MaxPageSize = 25;
        private readonly ILogger _logger = Startup.Logger;

        private readonly IServerPackageRepositoryFactory _repositoryFactory = Startup.OctopusProjectPackageRepositoryFactory;

        // GET /Packages(Id=,Version=)
        [HttpGet]
        public async Task<IHttpActionResult> Get(ODataQueryOptions<ODataPackage> options, string id, string version, CancellationToken token)
        {
            _logger.Info($"NuGetODataController.Get: {Request.RequestUri}");
            var serverRepository = _repositoryFactory.GetPackageRepository(User);
            if (!serverRepository.IsAuthenticated)
                return StatusCode(HttpStatusCode.Forbidden);

            var package = await serverRepository.GetProjectReleaseAsync(id, version, token);
            if (package == null)
                return NotFound();

            return TransformToQueryResult(options, new[] {package}, ClientCompatibility.Max).FormattedAsSingleResult<ODataPackage>();
        }

        // GET/POST /FindPackagesById()?id=
        [HttpGet]
        [HttpPost]
        public async Task<IHttpActionResult> FindPackagesById(ODataQueryOptions<ODataPackage> options, [FromODataUri] string id, [FromUri] string semVerLevel = "", CancellationToken token = default(CancellationToken))
        {
            _logger.Info($"NuGetODataController.FindPackagesById: {Request.RequestUri}");
            var serverRepository = _repositoryFactory.GetPackageRepository(User);
            if (!serverRepository.IsAuthenticated)
                return StatusCode(HttpStatusCode.Forbidden);

            if (string.IsNullOrEmpty(id))
                return QueryResult(options, Enumerable.Empty<ODataPackage>().AsQueryable(), MaxPageSize);

            var sourceQuery = await serverRepository.GetProjectReleasesAsync(id, token);

            return TransformToQueryResult(options, sourceQuery, ClientCompatibilityFactory.FromProperties(semVerLevel));
        }


        // GET /Packages(Id=,Version=)/propertyName
        [HttpGet]
        public IHttpActionResult GetPropertyFromPackages(string propertyName, string id, string version)
        {
            _logger.Info($"NuGetODataController.GetPropertyFromPackages: {Request.RequestUri}");

            switch (propertyName.ToLowerInvariant())
            {
                case "id": return Ok(id);
                case "version": return Ok(version);
                default: return BadRequest($"Querying property {propertyName} is not supported.");
            }
        }

        // GET/POST /Search()?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        [HttpPost]
        public async Task<IHttpActionResult> Search(ODataQueryOptions<ODataPackage> options, [FromODataUri] string searchTerm = "", [FromODataUri] string targetFramework = "", [FromODataUri] bool includePrerelease = false, [FromODataUri] bool includeDelisted = false, [FromUri] string semVerLevel = "", CancellationToken token = default(CancellationToken))
        {
            _logger.Info($"NuGetODataController.Search: {Request.RequestUri}");
            var serverRepository = _repositoryFactory.GetPackageRepository(User);
            if (!serverRepository.IsAuthenticated)
                return StatusCode(HttpStatusCode.Forbidden);

            var sourceQuery = await serverRepository.FindProjectsAsync(searchTerm, token);

            return TransformToQueryResult(options, sourceQuery, ClientCompatibilityFactory.FromProperties(semVerLevel));
        }

        // GET /Search()/$count?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        public async Task<IHttpActionResult> SearchCount(ODataQueryOptions<ODataPackage> options, [FromODataUri] string searchTerm = "", [FromODataUri] string targetFramework = "", [FromODataUri] bool includePrerelease = false, [FromODataUri] bool includeDelisted = false, [FromUri] string semVerLevel = "", CancellationToken token = default(CancellationToken))
        {
            _logger.Info($"NuGetODataController.SearchCount: {Request.RequestUri}");
            var serverRepository = _repositoryFactory.GetPackageRepository(User);
            if (!serverRepository.IsAuthenticated)
                return StatusCode(HttpStatusCode.Forbidden);

            var sourceQuery = await serverRepository.FindProjectsAsync(searchTerm, token);

            return TransformToQueryResult(options, sourceQuery, ClientCompatibilityFactory.FromProperties(semVerLevel))
                .FormattedAsCountResult<ODataPackage>();
        }


        // Exposed as OData Action for specific entity GET/HEAD /Packages(Id=,Version=)/Download
        [HttpGet]
        [HttpHead]
        public async Task<HttpResponseMessage> Download(string id, string version = "", CancellationToken token = default(CancellationToken))
        {
            _logger.Info($"NuGetODataController.Download: {Request.RequestUri}");
            var serverRepository = _repositoryFactory.GetPackageRepository(User);
            if (!serverRepository.IsAuthenticated)
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Not authenticated");

            var requestedPackage = await serverRepository.GetProjectReleaseAsync(id, version, token);
            if (requestedPackage == null)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, $"'Package {id} {version}' Not found.");

            var responseMessage = Request.CreateResponse(HttpStatusCode.OK);

            if (Request.Method == HttpMethod.Get)
                responseMessage.Content = new StreamContent(requestedPackage.GetStream());
            else
                responseMessage.Content = new StringContent(string.Empty);

            responseMessage.Content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("binary/octet-stream");
            responseMessage.Content.Headers.LastModified = requestedPackage.LastUpdated;
            responseMessage.Headers.ETag = new EntityTagHeaderValue($"\"{requestedPackage.PackageHash}\"");

            responseMessage.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(DispositionTypeNames.Attachment)
            {
                FileName = $"{requestedPackage.Id}.{requestedPackage.Version}{Constants.PackageExtension}",
                Size = requestedPackage.PackageSize,
                ModificationDate = responseMessage.Content.Headers.LastModified
            };

            return responseMessage;
        }

        private IHttpActionResult QueryResult<TModel>(ODataQueryOptions<TModel> options, IQueryable<TModel> queryable, int maxPageSize)
        {
            return new QueryResult<TModel>(options, queryable, this, maxPageSize);
        }

        private IHttpActionResult TransformToQueryResult(ODataQueryOptions<ODataPackage> options, IEnumerable<IServerPackage> sourceQuery, ClientCompatibility compatibility)
        {
            return QueryResult(options, sourceQuery.Distinct().Select(x => x.AsODataPackage(compatibility)).AsQueryable(), MaxPageSize);
        }
    }
}