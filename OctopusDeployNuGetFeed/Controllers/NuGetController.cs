using System.Collections.Generic;
using System.IO;
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
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.OData;
using OctopusDeployNuGetFeed.OWIN;
using OctopusDeployNuGetFeed.Services.ProjectRepository;
using OctopusDeployNuGetFeed.Services.ReleaseRepository;

namespace OctopusDeployNuGetFeed.Controllers
{
    [NuGetODataController]
    [Authorize(Roles = "Authenticated")]
    public class NuGetController : ODataController
    {
        private const int MaxPageSize = 25;
        private readonly IProjectRepositoryFactory _projectRepositoryFactory;
        private readonly IReleaseRepositoryFactory _releaseRepositoryFactory;

        public NuGetController(IProjectRepositoryFactory projectRepositoryFactory, IReleaseRepositoryFactory releaseRepositoryFactory)
        {
            _projectRepositoryFactory = projectRepositoryFactory;
            _releaseRepositoryFactory = releaseRepositoryFactory;
        }

        private IReleaseRepository ReleaseRepository => _releaseRepositoryFactory.GetReleaseRepository(OctopusCredential.FromPrincipal(User));
        private IProjectRepository ProjectRepository => _projectRepositoryFactory.GetProjectRepository(OctopusCredential.FromPrincipal(User));

        // GET /Packages(Id=,Version=)
        [HttpGet]
        [GzipCompressed]
        public async Task<IHttpActionResult> Get(ODataQueryOptions<ODataPackage> options, string id, string version, CancellationToken token)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
                return BadRequest();

            var package = await ReleaseRepository.GetReleaseAsync(id, version);

            return TransformToSingleResult(options, package);
        }

        // GET/POST /FindPackagesById()?id=
        [HttpGet]
        [HttpPost]
        [GzipCompressed]
        public async Task<IHttpActionResult> FindPackagesById(ODataQueryOptions<ODataPackage> options, [FromODataUri] string id, CancellationToken token)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest();

            var releases = await ReleaseRepository.GetAllReleasesAsync(id);

            return TransformToQueryResult(options, releases);
        }

        // GET/POST /Search()?searchTerm=&includePrerelease=
        [HttpGet]
        [HttpPost]
        [GzipCompressed]
        public async Task<IHttpActionResult> Search(ODataQueryOptions<ODataPackage> options, [FromODataUri] string searchTerm, CancellationToken token)
        {
            if (await ProjectRepository.ExistsAsync(searchTerm))
                return TransformToSingleResult(options, await ReleaseRepository.FindLatestReleaseAsync(searchTerm));

            var projectList = await ProjectRepository.GetAllProjectsAsync();
            var searchResult = projectList.Where(project => project.Listed && project.Title.WildcardMatch($"*{searchTerm}*"));

            return TransformToQueryResult(options, searchResult);
        }

        // Exposed as OData Action for specific entity GET/HEAD /Packages(Id=,Version=)/Download
        [HttpGet]
        [HttpHead]
        public async Task<HttpResponseMessage> Download(string id, string version, CancellationToken token)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
                return Request.CreateResponse(HttpStatusCode.BadRequest);

            var requestedPackage = await ReleaseRepository.GetPackageAsync(id, version);
            if (requestedPackage == null)
                return Request.CreateResponse(HttpStatusCode.NotFound);

            var responseMessage = Request.CreateResponse(HttpStatusCode.OK);

            if (Request.Method == HttpMethod.Get)
                responseMessage.Content = new StreamContent(new MemoryStream(requestedPackage.PackageBlob));
            else
                responseMessage.Content = new StringContent(string.Empty);

            responseMessage.Content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("binary/octet-stream");

            responseMessage.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(DispositionTypeNames.Attachment)
            {
                FileName = $"{requestedPackage.Id}.{requestedPackage.Version}.{Constants.PackageExtension}",
                Size = requestedPackage.PackageBlobSize,
                ModificationDate = responseMessage.Content.Headers.LastModified
            };

            return responseMessage;
        }

        private IHttpActionResult TransformToQueryResult(ODataQueryOptions<ODataPackage> options, IEnumerable<ODataPackage> sourceQuery)
        {
            return new QueryResult<ODataPackage>(options, sourceQuery.AsQueryable(), this, MaxPageSize);
        }

        private IHttpActionResult TransformToSingleResult(ODataQueryOptions<ODataPackage> options, ODataPackage source)
        {
            if (source == null)
                return NotFound();

            return new QueryResult<ODataPackage>(options, source.AsEnumerable().AsQueryable(), this, 1);
        }
    }
}