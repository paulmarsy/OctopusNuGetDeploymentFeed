using Owin;

namespace OctopusDeployNuGetFeed.OWIN
{
    public interface IOwinStartup
    {
        void Configuration(IAppBuilder app);
    }
}