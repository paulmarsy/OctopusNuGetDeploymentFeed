namespace OctopusDeployNuGetFeed.Octopus
{
    public interface IOctopusConnection
    {
        string BaseUri { get; }
        string ApiKey { get; }
        (bool created, string id) RegisterNuGetFeed(string host);
    }
}