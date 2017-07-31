using System;
using System.Web.Http;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using OctopusDeployNuGetFeed;
using OctopusDeployNuGetFeed.DataServices;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus;
using Owin;

[assembly: OwinStartup(typeof(Startup))]

namespace OctopusDeployNuGetFeed
{
    public class Startup
    {
        public static readonly ILogger Logger = new LogManager();

        public static string BaseAddress => $"http://{Program.Host}:{Program.Port}/";
        public static IServerPackageRepositoryFactory OctopusProjectPackageRepositoryFactory { get; } = new OctopusProjectPackageRepositoryFactory();

        public IDisposable App { get; private set; }

        public void Configuration(IAppBuilder appBuilder)
        {
            var config = new HttpConfiguration();
            config.UseNuGetV2WebApiFeed(
                "OctopusNuGetDeploymentFeed",
                "nuget",
                "NuGetOData");
            Logger.Info($"NuGet V2 WebApi Feed {BaseAddress}nuget");

            config.Routes.MapHttpRoute(
                "ResourceNotFound",
                "{*uri}",
                new {controller = "Default", uri = RouteParameter.Optional});

            appBuilder.Use<GlobalExceptionMiddleware>();
            appBuilder.Use<BasicAuthentication>();
            appBuilder.UseWebApi(config);
        }

        public void Start()
        {
            Logger.Info($"Command line switches: -host:{Program.Host} -port:{Program.Port}");

            Logger.Info($"Host: {Program.Host}");
            Logger.Info($"Port: {Program.Port}");

            Logger.Info("Starting WebApp...");
            App = WebApp.Start<Startup>(BaseAddress);
            Logger.Info($"Listening on {BaseAddress}");
        }

        public void Stop()
        {
            App.Dispose();
        }
    }
}