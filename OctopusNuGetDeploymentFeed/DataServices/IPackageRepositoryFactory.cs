using System.Security.Principal;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IPackageRepositoryFactory
    {
        IPackageRepository GetPackageRepository(IPrincipal user);
    }
}