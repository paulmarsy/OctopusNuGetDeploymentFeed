using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Services.ProjectRepository
{
    public interface IProjectRepositoryFactory
    {
        IProjectRepository GetProjectRepository(OctopusCredential credential);
    }
}