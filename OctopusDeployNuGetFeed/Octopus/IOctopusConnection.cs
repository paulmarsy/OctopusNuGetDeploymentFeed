using System.Threading.Tasks;
using Octopus.Client;

namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IOctopusConnection
    {
        string BaseUri { get; }
        string ApiKey { get; }
        Task<(bool created, string id)> RegisterNuGetFeed(string host);
        IOctopusAsyncRepository GetRepository(string operation, string target);
        IOctopusAsyncClient GetClient(string operation, string target);
    }
}