// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
using NuGet.Server.Core.DataServices;
using NuGet.Server.Core.Infrastructure;
using NuGet.Server.V2.Model;
using NuGet.Server.V2.OData;
using NuGet.Server.Core.Logging;

namespace NuGet.Server.V2.Controllers
{
    [NuGetODataControllerConfiguration]
    public abstract class NuGetODataController : ODataController
    {

        protected int _maxPageSize = 25;

        private readonly Core.Logging.ILogger _logger;
        protected readonly IServerPackageRepositoryFactory _repositoryFactory;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="repository">Required.</param>
        /// <param name="authenticationService">Optional. If this is not supplied Upload/Delete is not available (requests returns 403 Forbidden)</param>
        protected NuGetODataController(
            Core.Logging.ILogger logger,
            IServerPackageRepositoryFactory repository)
        {
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            _logger = logger;
            _repositoryFactory = repository;
        }
    

    

        // GET /Packages(Id=,Version=)
        [HttpGet]
        public virtual async Task<IHttpActionResult> Get(
            ODataQueryOptions<ODataPackage> options,
            string id,
            string version,
            CancellationToken token)
        {
            _logger.Log(LogLevel.Info, $"NuGetODataController.Get: {this.Request.RequestUri.ToString()}");
            using (var serverRepository = _repositoryFactory.GetPackageRepository(User))
            {
                if (!serverRepository.IsAuthenticated)
                    return this.StatusCode(HttpStatusCode.Forbidden);

                var package = (await serverRepository.GetPackagesAsync(id, version, token)).FirstOrDefault();

                if (package == null)
                {
                    return NotFound();
                }

                return TransformToQueryResult(options, new[] {package}, ClientCompatibility.Max)
                    .FormattedAsSingleResult<ODataPackage>();
            }
        }

        // GET/POST /FindPackagesById()?id=
        [HttpGet]
        [HttpPost]
        public virtual async Task<IHttpActionResult> FindPackagesById(
            ODataQueryOptions<ODataPackage> options,
            [FromODataUri] string id,
            [FromUri] string semVerLevel = "",
            CancellationToken token = default(CancellationToken))
        {
            _logger.Log(LogLevel.Info, $"NuGetODataController.FindPackagesById: {this.Request.RequestUri.ToString()}");

            if (string.IsNullOrEmpty(id))
            {
                var emptyResult = Enumerable.Empty<ODataPackage>().AsQueryable();
                return QueryResult(options, emptyResult, _maxPageSize);
            }

            var clientCompatibility = ClientCompatibilityFactory.FromProperties(semVerLevel);
            using (var serverRepository = _repositoryFactory.GetPackageRepository(User))
            {
                if (!serverRepository.IsAuthenticated)
                    return this.StatusCode(HttpStatusCode.Forbidden);

                var sourceQuery = await serverRepository.GetPackagesAsync(id, null, token);
                return TransformToQueryResult(options, sourceQuery, clientCompatibility);

            }
            
        }


        // GET /Packages(Id=,Version=)/propertyName
        [HttpGet]
        public virtual IHttpActionResult GetPropertyFromPackages(string propertyName, string id, string version)
        {
            _logger.Log(LogLevel.Info, $"NuGetODataController.GetPropertyFromPackages: {this.Request.RequestUri.ToString()}");

            switch (propertyName.ToLowerInvariant())
            {
                case "id": return Ok(id);
                case "version": return Ok(version);
            }

            return BadRequest("Querying property " + propertyName + " is not supported.");
        }

        // GET/POST /Search()?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        [HttpPost]
        public virtual async Task<IHttpActionResult> Search(
            ODataQueryOptions<ODataPackage> options,
            [FromODataUri] string searchTerm = "", 
            [FromODataUri] string targetFramework = "", 
            [FromODataUri] bool includePrerelease = false,
            [FromODataUri] bool includeDelisted = false,
            [FromUri] string semVerLevel = "",
            CancellationToken token = default(CancellationToken))
        {
            _logger.Log(LogLevel.Info, $"NuGetODataController.Search: {this.Request.RequestUri.ToString()}");
            

            var clientCompatibility = ClientCompatibilityFactory.FromProperties(semVerLevel);
            using (var serverRepository = _repositoryFactory.GetPackageRepository(User))
            {
                if (!serverRepository.IsAuthenticated)
                    return this.StatusCode(HttpStatusCode.Forbidden);

                var sourceQuery = await serverRepository.SearchAsync(
                    searchTerm,
                    includePrerelease,
                    token);

                return TransformToQueryResult(options, sourceQuery, clientCompatibility);
            }
        }

        // GET /Search()/$count?searchTerm=&targetFramework=&includePrerelease=
        [HttpGet]
        public virtual async Task<IHttpActionResult> SearchCount(
            ODataQueryOptions<ODataPackage> options,
            [FromODataUri] string searchTerm = "",
            [FromODataUri] string targetFramework = "",
            [FromODataUri] bool includePrerelease = false,
            [FromODataUri] bool includeDelisted = false,
            [FromUri] string semVerLevel = "",
            CancellationToken token = default(CancellationToken))
        {
            _logger.Log(LogLevel.Info, $"NuGetODataController.SearchCount: {this.Request.RequestUri.ToString()}");

            var searchResults = await Search(
                options,
                searchTerm,
                targetFramework,
                includePrerelease,
                includeDelisted,
                semVerLevel,
                token);

            return searchResults.FormattedAsCountResult<ODataPackage>();
        }



        /// <summary>
        /// Exposed as OData Action for specific entity
        /// GET/HEAD /Packages(Id=,Version=)/Download
        /// </summary>
        /// <param name="id"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        [HttpGet, HttpHead]
        public virtual async Task<HttpResponseMessage> Download(
            string id,
            string version = "",
            CancellationToken token = default(CancellationToken))
        {
            _logger.Log(LogLevel.Info, $"NuGetODataController.Download: {this.Request.RequestUri.ToString()}");
            using (var serverRepository = _repositoryFactory.GetPackageRepository(User))
            {
                if (!serverRepository.IsAuthenticated)
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden,"Not authenticated");

                var requestedPackage = (await serverRepository.GetPackagesAsync(id, version, token)).FirstOrDefault();

                if (requestedPackage == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, string.Format("'Package {0} {1}' Not found.", id, version));
                }

                var responseMessage = Request.CreateResponse(HttpStatusCode.OK);

                if (Request.Method == HttpMethod.Get)
                {
                    responseMessage.Content = new StreamContent(requestedPackage.GetStream());
                }
                else
                {
                    responseMessage.Content = new StringContent(string.Empty);
                }

                responseMessage.Content.Headers.ContentType = new MediaTypeWithQualityHeaderValue("binary/octet-stream");
                if (requestedPackage != null)
                {
                    responseMessage.Content.Headers.LastModified = requestedPackage.LastUpdated;
                    responseMessage.Headers.ETag = new EntityTagHeaderValue('"' + requestedPackage.PackageHash + '"');
                }

                responseMessage.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(DispositionTypeNames.Attachment)
                {
                    FileName = string.Format("{0}.{1}{2}", requestedPackage.Id, requestedPackage.Version, NuGet.Constants.PackageExtension),
                    Size = requestedPackage != null ? (long?) requestedPackage.PackageSize : null,
                    ModificationDate = responseMessage.Content.Headers.LastModified,
                };

                return responseMessage;
            }
        }

     

        protected HttpResponseMessage CreateStringResponse(HttpStatusCode statusCode, string response)
        {
            var responseMessage = new HttpResponseMessage(statusCode) { Content = new StringContent(response) };
            return responseMessage;
        }

    

        protected IQueryable<ODataPackage> TransformPackages(
            IEnumerable<IServerPackage> packages,
            ClientCompatibility compatibility)
        {
            return packages
                .Distinct()
                .Select(x => x.AsODataPackage(compatibility))
                .AsQueryable()
                .InterceptWith(new NormalizeVersionInterceptor());
        }

        /// <summary>
        /// Generates a QueryResult.
        /// </summary>
        /// <typeparam name="TModel">Model type.</typeparam>
        /// <param name="options">OData query options.</param>
        /// <param name="queryable">Queryable to build QueryResult from.</param>
        /// <param name="maxPageSize">Maximum page size.</param>
        /// <returns>A QueryResult instance.</returns>
        protected virtual IHttpActionResult QueryResult<TModel>(ODataQueryOptions<TModel> options, IQueryable<TModel> queryable, int maxPageSize)
        {
            return new QueryResult<TModel>(options, queryable, this, maxPageSize);
        }

        /// <summary>
        /// Transforms IPackages to ODataPackages and generates a QueryResult<ODataPackage></ODataPackage>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="sourceQuery"></param>
        /// <returns></returns>
        protected virtual IHttpActionResult TransformToQueryResult(
            ODataQueryOptions<ODataPackage> options,
            IEnumerable<IServerPackage> sourceQuery,
            ClientCompatibility compatibility)
        {
            var transformedQuery = TransformPackages(sourceQuery, compatibility);
            return QueryResult(options, transformedQuery, _maxPageSize);
        }
    }
}
