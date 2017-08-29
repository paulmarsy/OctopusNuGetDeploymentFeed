using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Services.ReleaseRepository
{
    public interface IReleaseRepositoryFactory
    {
        IReleaseRepository GetReleaseRepository(OctopusCredential credential);
    }
}