using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.Octopus;

namespace OctopusDeployNuGetFeed.Services.ProjectRepository
{
    public class OctopusProjectRepository : IProjectRepository
    {
        private readonly IOctopusServer _server;

        public OctopusProjectRepository(IOctopusServer server)
        {
            _server = server;
            _server.InitialisePreloader();
        }

        public Task<IEnumerable<ODataPackage>> GetAllProjectsAsync()
        {
            return Task.FromResult(GetAllProjects().Select(ODataPackage.FromNuGetPackage));
        }

        private IEnumerable<INuGetPackage> GetAllProjects()
        {
            return _server.GetAllProjects().Select(project => new SearchPackage(project));
        }
    }
}