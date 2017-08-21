using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Filters;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public class GzipCompressedAttribute : ActionFilterAttribute
    {
        private const string GZipEncoding = "gzip";

        public override async Task OnActionExecutedAsync(HttpActionExecutedContext actContext, CancellationToken token)
        {
            if (actContext.Request.Headers.AcceptEncoding.All(x => x.Value != GZipEncoding))
            {
                await base.OnActionExecutedAsync(actContext, token);
                return;
            }

            var contentStream = await actContext.Response.Content.ReadAsStreamAsync();

            actContext.Response.Content = new PushStreamContent(async (stream, content, context) =>
            {
                using (contentStream)
                using (var zipStream = new GZipStream(stream, CompressionLevel.Fastest))
                {
                    await contentStream.CopyToAsync(zipStream);
                }
            });

            actContext.Response.Content.Headers.Add("Content-Encoding", GZipEncoding);
        }
    }
}