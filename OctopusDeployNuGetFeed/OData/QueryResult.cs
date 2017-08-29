using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData.Query;
using System.Web.Http.Results;
using Microsoft.Data.OData;

namespace OctopusDeployNuGetFeed.OData
{
    public class QueryResult<TModel> : IHttpActionResult
    {
        private readonly ApiController _controller;
        private readonly int _maxPageSize;
        private readonly IQueryable<TModel> _queryable;
        private readonly ODataQueryOptions<TModel> _queryOptions;

        public QueryResult(ODataQueryOptions<TModel> queryOptions, IQueryable<TModel> queryable, ApiController controller, int maxPageSize)
        {
            _queryOptions = queryOptions;
            _queryable = queryable;
            _controller = controller;
            _maxPageSize = maxPageSize;
        }

        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await Execute().ExecuteAsync(cancellationToken);
            }
            catch (ODataException e)
            {
                return _controller.Request.CreateErrorResponse(HttpStatusCode.BadRequest, e);
            }
        }

        public IHttpActionResult Execute()
        {
            _queryOptions.Validate(new ODataValidationSettings
            {
                MaxNodeCount = 250
            });

            var queryResult = _queryOptions.ApplyTo(_queryable, new ODataQuerySettings
            {
                HandleNullPropagation = HandleNullPropagationOption.False,
                EnsureStableOrdering = true,
                EnableConstantParameterization = false,
                PageSize = _maxPageSize
            }) as IQueryable<TModel>;

            if (queryResult == null)
                return NotFoundResult();

            if (_maxPageSize == 1)
                return NegotiatedContentResult(queryResult.FirstOrDefault());

            return NegotiatedContentResult(queryResult);
        }

        private NotFoundResult NotFoundResult()
        {
            return new NotFoundResult(_controller.Request);
        }

        private OkNegotiatedContentResult<TResponseModel> NegotiatedContentResult<TResponseModel>(TResponseModel content)
        {
            return new OkNegotiatedContentResult<TResponseModel>(content, _controller);
        }
    }
}