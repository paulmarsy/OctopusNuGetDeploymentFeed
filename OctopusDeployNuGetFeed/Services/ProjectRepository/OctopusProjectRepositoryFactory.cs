using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Services.ProjectRepository
{
    public class OctopusProjectRepositoryFactory : IProjectRepositoryFactory
    {
        private readonly IOctopusClientFactory _factory;

        public OctopusProjectRepositoryFactory(IOctopusClientFactory factory)
        {
            _factory = factory;
        }

        public IProjectRepository GetProjectRepository(OctopusCredential credential)
        {
            return new OctopusProjectRepository(_factory.GetServer(credential));
        }
    }
}