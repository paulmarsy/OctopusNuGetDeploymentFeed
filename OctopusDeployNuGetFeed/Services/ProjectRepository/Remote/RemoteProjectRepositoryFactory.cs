using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Services.ProjectRepository.Remote
{
    public class RemoteProjectRepositoryFactory : IProjectRepositoryFactory
    {
        public IProjectRepository GetProjectRepository(OctopusCredential credential)
        {
            return new RemoteProjectRepositoryClient(credential);
        }
    }
}