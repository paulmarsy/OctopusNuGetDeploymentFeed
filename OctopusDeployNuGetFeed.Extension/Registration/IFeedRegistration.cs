namespace OctopusDeployNuGetFeed.Extension.Registration
{
    public interface IFeedRegistration
    {
        void Register(string listenPrefix);
    }
}