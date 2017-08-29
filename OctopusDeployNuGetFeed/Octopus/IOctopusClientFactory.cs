using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IOctopusClientFactory
    {
        int RegisteredOctopusServers { get; }
        Task<bool> IsAuthenticated(OctopusCredential credential);
        IOctopusConnection GetConnection(OctopusCredential credential);
        IOctopusServer GetServer(OctopusCredential credential);
        void Reset();
    }
}