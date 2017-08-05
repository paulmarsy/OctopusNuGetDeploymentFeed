namespace OctopusDeployNuGetFeed.DataServices
{
    public interface IOctopusServer
    {
        bool IsAuthenticated { get; }
        string BaseUri { get; }
        string ApiKey { get; }
    }
}