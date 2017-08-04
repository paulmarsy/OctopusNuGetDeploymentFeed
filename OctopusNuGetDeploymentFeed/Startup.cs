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
        public static readonly ILogger Logger = LogManager.Current;

        public static string BaseAddress => $"http://{Program.Host}:{Program.Port}/";
        public static IPackageRepositoryFactory OctopusProjectPackageRepositoryFactory { get; } = new OctopusPackageRepositoryFactory();

        public IDisposable App { get; private set; }

        public void Configuration(IAppBuilder appBuilder)
        {
            appBuilder.Use<GlobalExceptionMiddleware>();
            appBuilder.Use<BasicAuthentication>();

            var config = new HttpConfiguration();

            config.Routes.MapHttpRoute(
                "HomePage",
                "",
                new { controller = "Default",action= "Index"});

            config.UseNuGetV2WebApiFeed(
                "OctopusNuGetDeploymentFeed",
                "nuget",
                "NuGetOData");

            config.Routes.MapHttpRoute(
                "ResourceNotFound",
                "{*uri}",
                new {controller = "Default", uri = RouteParameter.Optional});

            appBuilder.UseWebApi(config);
        }

        public void Start()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            Logger.Info($"Command line switches: -host:{Program.Host} -port:{Program.Port}");

            Logger.Info($"Host: {Program.Host}");
            Logger.Info($"Port: {Program.Port}");

            Logger.Info("Starting WebApp...");
            App = WebApp.Start<Startup>(BaseAddress);
            Logger.Info($"Listening on {BaseAddress}");
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
        {
            var excepion = unhandledExceptionEventArgs.ExceptionObject as Exception;
            Logger.Error($"Unhandled Exception!!: {excepion?.Message}. {excepion?.InnerException?.Message}\n{excepion?.StackTrace}");
        }

        public void Stop()
        {
            App.Dispose();
        }
    }
}