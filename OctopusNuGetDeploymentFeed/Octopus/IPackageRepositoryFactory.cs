using System.Security.Principal;

namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IPackageRepositoryFactory
    {
        int Count { get; }
        bool IsAuthenticated(string username, string password);
        IPackageRepository GetPackageRepository(IPrincipal user);
        IOctopusServer GetServer(IPrincipal user);
        IOctopusCache GetCache(IPrincipal user);
        void Reset();
    }
}