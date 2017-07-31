using System.Security.Principal;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IServerPackageRepositoryFactory
    {
        IServerPackageRepository GetPackageRepository(IPrincipal user);
    }
}