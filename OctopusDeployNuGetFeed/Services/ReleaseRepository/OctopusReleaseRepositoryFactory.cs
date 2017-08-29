using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Services.ReleaseRepository
{
    public class OctopusReleaseRepositoryFactory : IReleaseRepositoryFactory
    {
        private readonly IOctopusClientFactory _factory;

        public OctopusReleaseRepositoryFactory(IOctopusClientFactory factory)
        {
            _factory = factory;
        }

        public IReleaseRepository GetReleaseRepository(OctopusCredential credential)
        {
            return new OctopusReleaseRepository(_factory.GetConnection(credential), _factory.GetServer(credential));
        }
    }
}