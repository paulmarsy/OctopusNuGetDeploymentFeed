namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IOctopusServer
    {
        string BaseUri { get; }
        string ApiKey { get; }
        (bool created, string id) RegisterNuGetFeed(string host);
    }
}