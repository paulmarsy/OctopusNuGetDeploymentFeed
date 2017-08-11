using System;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using ApplicationInsights.OwinExtensions;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using OctopusDeployNuGetFeed;
using OctopusDeployNuGetFeed.Infrastructure;
using OctopusDeployNuGetFeed.Logging;
using Owin;

[assembly: OwinStartup(typeof(Startup))]

namespace OctopusDeployNuGetFeed
{
    public class Startup
    {
        private IDisposable _webApiApp;

        public void Configuration(IAppBuilder app)
        {
            var config = new HttpConfiguration();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(Program.Container);

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


            config.Services.Replace(typeof(IExceptionHandler), new PassthroughExceptionHandler(Program.Container.Resolve<ILogger>()));

            app.Use(async (ctx, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception e)
                {
                    Program.Container.Resolve<ILogger>().UnhandledException(e);
#if DEBUG
                    throw;
#endif
                }
            });
            if (Program.Container.Resolve<IAppInsights>().IsEnabled)
                app.UseApplicationInsights(shouldTraceRequest: ShouldTraceRequest);

            app.Use<BasicAuthentication>();
            app.UseWebApi(config);
        }

        private static bool ShouldTraceRequest(IOwinRequest request, IOwinResponse response)
        {
            // Avoid tracing '401 Forbidden' otherwise 50% of all requests show as failed requests
            return response.StatusCode != 401;
        }

        public void Start(ILogger logger)
        {
            logger.Info($"Host: {Program.Host}");
            logger.Info($"Port: {Program.Port}");

            logger.Info("Starting WebApp...");
            _webApiApp = WebApp.Start<Startup>(Program.BaseAddress);
            logger.Info($"Listening on {Program.BaseAddress}");
        }

        public void Stop()
        {
            _webApiApp.Dispose();
        }
    }
}