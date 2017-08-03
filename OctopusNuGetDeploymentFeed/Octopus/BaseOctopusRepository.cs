using Octopus.Client;

namespace OctopusDeployNuGetFeed.Octopus
{
    public abstract class BaseOctopusRepository
    {
        private IOctopusAsyncClient _client;

        private OctopusServerEndpoint _endpoint;

        protected BaseOctopusRepository(string baseUri, string apiKey)
        {
            BaseUri = baseUri;
            ApiKey = apiKey;
        }


        internal OctopusServerEndpoint Endpoint => _endpoint ?? (_endpoint = new OctopusServerEndpoint(BaseUri, ApiKey));
        internal IOctopusAsyncClient Client => _client ?? (_client = OctopusAsyncClient.Create(Endpoint).GetAwaiter().GetResult());
        public string BaseUri { get; }
        public string ApiKey { get; }

        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(BaseUri) || string.IsNullOrWhiteSpace(ApiKey))
                        return false;

                    return Endpoint != null && Client != null;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}