using System.Security.Principal;
using OctopusDeployNuGetFeed.Octopus.ProjectCache;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IServerPackageRepositoryFactory
    {
        IServerPackageRepository GetPackageRepository(IPrincipal user);
        OctopusProjectCache GetPackageCache(IServerPackageRepository repository);
    }
}