using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public class BasicAuthentication : OwinMiddleware
    {
        public BasicAuthentication(OwinMiddleware next) :
            base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            var response = context.Response;
            var request = context.Request;

            response.OnSendingHeaders(state =>
            {
                var owinResponse = (OwinResponse)state;

                if (owinResponse.StatusCode == 401)
                    owinResponse.Headers.Add("WWW-Authenticate", new[] { "Basic" });
            }, response);

            var header = request.Headers["Authorization"];

            if (!string.IsNullOrWhiteSpace(header))
            {
                var authHeader = AuthenticationHeaderValue.Parse(header);

                if ("Basic".Equals(authHeader.Scheme, StringComparison.OrdinalIgnoreCase))
                {
                    var parameter = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter));

                    var lastColonIndex = parameter.LastIndexOf(':');
                    if (lastColonIndex != -1)
                    {
                        var username = parameter.Substring(0, lastColonIndex).Trim();
                        var password = parameter.Substring(lastColonIndex + 1).Trim();

                        if (ValidateUser(username, password))
                            SetClaimsIdentity(request, username, password);
                    }
                }
            }

            await Next.Invoke(context);
        }

        protected virtual void SetClaimsIdentity(IOwinRequest request, string username, string password)
        {
            var id = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Uri, username),
                new Claim(ClaimTypes.UserData, password)
            }, "Basic");

            request.User = new ClaimsPrincipal(id);
        }

        protected virtual bool ValidateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return false;

            return true;
        }
    }
}