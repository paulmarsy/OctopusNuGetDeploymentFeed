using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusCredential
    {
        public OctopusCredential(string baseUri, string apiKey)
        {
            BaseUri = baseUri;
            ApiKey = apiKey;
        }

        public string BaseUri { get; }
        public string ApiKey { get; }

        public string GetHost()
        {
            try
            {
                return new Uri(BaseUri).Host;
            }
            catch
            {
                return null;
            }
        }

        public static OctopusCredential FromPrincipal(IPrincipal principal)
        {
            var claimsPrincipal = principal as ClaimsPrincipal;
            var baseUri = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.Uri)?.Value;
            var apiKey = claimsPrincipal?.Claims.SingleOrDefault(claim => claim.Type == ClaimTypes.UserData)?.Value;

            return new OctopusCredential(baseUri, apiKey);
        }
    }
}