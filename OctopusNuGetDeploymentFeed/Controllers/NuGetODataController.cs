using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.Octopus.Packages;
using OctopusDeployNuGetFeed.OData;

namespace OctopusDeployNuGetFeed.Controllers
{
    [NuGetODataControllerConfiguration]
    [Authorize(Roles = "Authenticated")]
    public class NuGetODataController : ODataController
    {
        private const int MaxPageSize = 25;
        private readonly IPackageRepositoryFactory _packageRepositoryFactory;

        public NuGetODataController(IPackageRepositoryFactory packageRepositoryFactory)
        {
            _packageRepositoryFactory = packageRepositoryFactory;
        }

        private IPackageRepository Repository => _packageRepositoryFactory.GetPackageRepository(User);

        // GET /Packages(Id=,Version=)
        [HttpGet]
        [GzipCompressed]
        public IHttpActionResult Get(ODataQueryOptions<ODataPackage> options, string id, string version, CancellationToken token)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
                return BadRequest();

            var package = Repository.GetRelease(id, version, token);
            if (package == null)
                return NotFound();

            return TransformToQueryResult(options, new[] {package}).FormattedAsSingleResult<ODataPackage>();
        }

        // GET/POST /FindPackagesById()?id=
        [HttpGet]
        [HttpPost]
        [GzipCompressed]
        public IHttpActionResult FindPackagesById(ODataQueryOptions<ODataPackage> options, [FromODataUri] string id, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest();

            var sourceQuery = Repository.FindProjectReleases(id, token);

            return TransformToQueryResult(options, sourceQuery);
        }

        // GET/POST /Search()?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        [HttpPost]
        [GzipCompressed]
        public IHttpActionResult Search(ODataQueryOptions<ODataPackage> options, [FromODataUri] string searchTerm = "", [FromODataUri] bool includePrerelease = false, [FromODataUri] bool includeDelisted = false, CancellationToken token = default(CancellationToken))
        {
            var sourceQuery = Repository.FindProjects(searchTerm, token).Where(package => package.Listed || package.Listed == false && includeDelisted);

            return TransformToQueryResult(options, sourceQuery);
        }

        // Exposed as OData Action for specific entity GET/HEAD /Packages(Id=,Version=)/Download
        [HttpGet]
        [HttpHead]
        public HttpResponseMessage Download(string id, string version, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
                return Request.CreateResponse(HttpStatusCode.BadRequest);

            var requestedPackage = Repository.GetRelease(id, version, token);
            if (requestedPackage == null)
                return Request.CreateResponse(HttpStatusCode.NotFound);

            var responseMessage = Request.CreateResponse(HttpStatusCode.OK);

            if (Request.Method == HttpMethod.Get)
                responseMessage.Content = new StreamContent(requestedPackage.GetStream());
            else
                responseMessage.Content = new StringContent(string.Empty);

            responseMessage.Content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("binary/octet-stream");
            responseMessage.Content.Headers.LastModified = requestedPackage.Published;

            responseMessage.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(DispositionTypeNames.Attachment)
            {
                FileName = $"{requestedPackage.Id}.{requestedPackage.Version}.{Constants.PackageExtension}",
                Size = requestedPackage.PackageSize,
                ModificationDate = responseMessage.Content.Headers.LastModified
            };

            return responseMessage;
        }

        private IHttpActionResult TransformToQueryResult(ODataQueryOptions<ODataPackage> options, IEnumerable<INuGetPackage> sourceQuery)
        {
            return new QueryResult<ODataPackage>(options, sourceQuery.Distinct().Select(AsODataPackage).AsQueryable(), this, MaxPageSize);
        }

        private static ODataPackage AsODataPackage(INuGetPackage package)
        {
            return new ODataPackage
            {
                Id = package.Id,
                Version = package.Version,
                Summary = package.Summary,
                IsAbsoluteLatestVersion = package.IsAbsoluteLatestVersion,
                IsLatestVersion = package.IsLatestVersion,
                Published = package.Published,
                Authors = package.Authors,
                Description = package.Description,
                Listed = package.Listed,
                ReleaseNotes = package.ReleaseNotes,
                Title = package.Title
            };
        }
    }
}