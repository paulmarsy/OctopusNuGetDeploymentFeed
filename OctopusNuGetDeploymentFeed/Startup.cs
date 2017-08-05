using System;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using ApplicationInsights.OwinExtensions;
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
        private readonly ILogger _logger = LogManager.Current;

        private IDisposable _webApiApp;

        public static IPackageRepositoryFactory OctopusProjectPackageRepositoryFactory { get; } = new OctopusPackageRepositoryFactory();
        public static AppInsights AppInsights { get; } = new AppInsights();

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


            config.Services.Replace(typeof(IExceptionHandler), new PassthroughExceptionHandler(LogManager.Current));

            app.Use(async (ctx, next) =>
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
            });
            if (AppInsights.IsEnabled)
                app.UseApplicationInsights();

            app.Use<BasicAuthentication>();
            app.UseWebApi(config);
        }

        public void Start()
        {
            AppInsights.Initialize();

            _logger.Info($"Host: {Program.Host}");
            _logger.Info($"Port: {Program.Port}");

            _logger.Info("Starting WebApp...");
            _webApiApp = WebApp.Start<Startup>(Program.BaseAddress);
            _logger.Info($"Listening on {Program.BaseAddress}");
        }


        public void Stop()
        {
            _webApiApp.Dispose();
        }
    }
}