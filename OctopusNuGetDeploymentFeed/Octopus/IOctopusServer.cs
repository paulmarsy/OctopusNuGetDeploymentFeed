namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IOctopusServer
    {
        bool IsAuthenticated { get; }
        string BaseUri { get; }
        string ApiKey { get; }
        (bool created, string id) RegisterNuGetFeed(string host);
    }
}