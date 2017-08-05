using System;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
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
        public static readonly ILogger Logger = LogManager.Current;

        public static IPackageRepositoryFactory OctopusProjectPackageRepositoryFactory { get; } = new OctopusPackageRepositoryFactory();

        public IDisposable App { get; private set; }

        public void Configuration(IAppBuilder app)
        {
            var config = new HttpConfiguration();

            config.Routes.MapHttpRoute(
                "HomePage",
                "",
                new {controller = "Default", action = "Index"});

            config.UseNuGetV2WebApiFeed(
                "OctopusNuGetDeploymentFeed",
                "nuget",
                "NuGetOData");

            config.Routes.MapHttpRoute(
                "Default",
                "{*uri}",
                new {controller = "Default", uri = RouteParameter.Optional});


            config.Services.Replace(typeof(IExceptionHandler), new PassthroughExceptionHandler());

            app
                .Use(async (ctx, next) =>
                {
                    try
                    {
                        await next();
                    }
                    catch (Exception e)
                    {
                        LogManager.Current.UnhandledException(e);
#if DEBUG
            throw; 
#endif
                    }
                })
                .Use<BasicAuthentication>()
                .UseWebApi(config);
        }

        public void Start()
        {
            Logger.Info($"Command line switches: -host:{Program.Host} -port:{Program.Port}");

            Logger.Info($"Host: {Program.Host}");
            Logger.Info($"Port: {Program.Port}");

            Logger.Info("Starting WebApp...");
            App = WebApp.Start<Startup>(Program.BaseAddress);
            Logger.Info($"Listening on {Program.BaseAddress}");
        }


        public void Stop()
        {
            App.Dispose();
        }
    }
}