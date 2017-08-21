using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
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
            var logger = Program.Container.Resolve<ILogger>();

            if (Environment.UserInteractive)
            {
                app.Use(async (context, next) =>
                {
                    logger.Verbose($"{context.Request.Method} {context.Request.Uri}");

                    await next();
                });
            }
            if (Program.Container.Resolve<IAppInsights>().IsEnabled)
                app.UseApplicationInsights(new RequestTrackingConfiguration
                {
                    ShouldTrackRequest = ShouldTrackRequest
                });
            
            app.Use<BasicAuthentication>();

            var config = new HttpConfiguration();

            config.Services.Replace(typeof(IExceptionHandler), new PassthroughExceptionHandler(logger));
            config.DependencyResolver = new AutofacWebApiDependencyResolver(Program.Container);
            config.Formatters.JsonFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/html"));

            config.Routes.MapHttpRoute(
                "HomePage",
                "",
                new { controller = "Default", action = "Index" });

            config.Routes.MapHttpRoute(
                "Admin",
                "admin/{action}",
                new { controller = "Admin" });

            config.UseNuGetV2WebApiFeed(
                "OctopusNuGetDeploymentFeed",
                "nuget",
                "NuGetOData");

            config.Routes.MapHttpRoute(
                "Default",
                "{*uri}",
                new { controller = "Default", uri = RouteParameter.Optional });

            app.UseWebApi(config);
        }

        private static Task<bool> ShouldTrackRequest(IOwinContext owinContext)
        {
            // Avoid tracing '401 Forbidden' otherwise 50% of all requests show as failed requests
            return Task.FromResult(owinContext.Response.StatusCode != 401);
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