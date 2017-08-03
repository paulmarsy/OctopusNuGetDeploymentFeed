using System.Security.Principal;
using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IServerPackageRepositoryFactory
    {
        IServerPackageRepository GetPackageRepository(IPrincipal user);
        OctopusProjectCache GetPackageCache(IServerPackageRepository repository);
    }
}