using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public class PlainTextResult
        : IHttpActionResult
    {
        private readonly HttpRequestMessage _request;

        public PlainTextResult(string content, HttpRequestMessage request)
        {
            _request = request;
            Content = content;
        }

        public string Content { get; }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage
            {
                Content = new StringContent(Content, Encoding.UTF8, "text/plain"),
                RequestMessage = _request
            };
            return Task.FromResult(response);
        }
    }
}