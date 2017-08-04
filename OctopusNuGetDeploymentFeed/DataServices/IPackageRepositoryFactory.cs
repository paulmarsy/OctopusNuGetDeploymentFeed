using System.Security.Principal;
using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IPackageRepositoryFactory
    {
        IPackageRepository GetPackageRepository(IPrincipal user);
    }
}