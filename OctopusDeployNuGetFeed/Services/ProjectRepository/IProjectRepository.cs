using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;
using OctopusDeployNuGetFeed.Model;

namespace OctopusDeployNuGetFeed.Services.ProjectRepository
{
    public interface IProjectRepository : IService
    {
        Task<IEnumerable<ODataPackage>> GetAllProjectsAsync();
    }
}