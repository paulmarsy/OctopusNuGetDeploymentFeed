using System.Security.Principal;

namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IPackageRepositoryFactory
    {
        bool IsAuthenticated(string username, string password);
        IPackageRepository GetPackageRepository(IPrincipal user);
        IOctopusServer GetServer(IPrincipal user);
        IOctopusCache GetCache(IPrincipal user);
    }
}