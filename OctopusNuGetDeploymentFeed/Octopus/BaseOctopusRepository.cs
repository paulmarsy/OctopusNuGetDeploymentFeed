using System;
using Octopus.Client;
using OctopusDeployNuGetFeed.DataServices;

namespace OctopusDeployNuGetFeed.Octopus
{
    public class OctopusServer :IOctopusServer
    {
        private IHttpOctopusClient _client;
        private IOctopusRepository _repository;
        private readonly Lazy<OctopusServerEndpoint> _endpoint;

        public OctopusServer(string baseUri, string apiKey)
        {
            _endpoint = new Lazy<OctopusServerEndpoint>(() => new OctopusServerEndpoint(baseUri, apiKey));
        }


        internal IHttpOctopusClient Client => _client ?? (_client = new OctopusClient(_endpoint.Value));
        internal IOctopusRepository Repository => _repository ?? (_repository = new OctopusRepository(Client));
        public string BaseUri => _endpoint.Value.OctopusServer.ToString();
        public string ApiKey => _endpoint.Value.ApiKey;

        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    return Repository.Client.RefreshRootDocument() != null;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}