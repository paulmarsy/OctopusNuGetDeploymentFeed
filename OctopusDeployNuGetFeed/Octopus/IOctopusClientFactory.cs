namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IOctopusClientFactory
    {
        int RegisteredOctopusServers { get; }
        bool IsAuthenticated(OctopusCredential credential);
        IOctopusConnection GetConnection(OctopusCredential credential);
        IOctopusServer GetServer(OctopusCredential credential);
        void Reset();
    }
}