using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Services.AdminActor.Fabric
{
    public class AdminService : IAdminService
    {
        private readonly IOctopusClientFactory _octopusClientFactory;

        public AdminService(IOctopusClientFactory octopusClientFactory)
        {
            _octopusClientFactory = octopusClientFactory;
        }

        public void Decache()
        {
            _octopusClientFactory.Reset();
        }
    }
}