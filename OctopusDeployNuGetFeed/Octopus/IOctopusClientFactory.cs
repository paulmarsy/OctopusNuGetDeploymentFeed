using System.Threading.Tasks;
using OctopusDeployNuGetFeed.Services.ControlService;

namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IOctopusClientFactory : IServiceControl
    {
        int RegisteredOctopusServers { get; }
        Task<bool> IsAuthenticated(OctopusCredential credential);
        IOctopusConnection GetConnection(OctopusCredential credential);
        IOctopusServer GetServer(OctopusCredential credential);
    }
}