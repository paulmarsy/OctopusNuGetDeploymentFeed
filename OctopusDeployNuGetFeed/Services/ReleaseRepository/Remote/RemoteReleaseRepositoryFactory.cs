using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Services.ReleaseRepository.Remote
{
    public class RemoteReleaseRepositoryFactory : IReleaseRepositoryFactory
    {
        public IReleaseRepository GetReleaseRepository(OctopusCredential credential)
        {
            return new RemoteReleaseRepositoryClient(credential);
        }
    }
}