using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.ExceptionHandling;
using System.Web.Http.OData.Builder;
using System.Web.Http.OData.Extensions;
using System.Web.Http.OData.Routing;
using System.Web.Http.OData.Routing.Conventions;
using ApplicationInsights.OwinExtensions;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.Data.Edm;
using Microsoft.Data.OData;
using Microsoft.Owin;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Model;
using OctopusDeployNuGetFeed.OData.Conventions;
using OctopusDeployNuGetFeed.OWIN;
using OctopusDeployNuGetFeed.Services.NuGetFeed;
using Owin;

[assembly: OwinStartup(typeof(NuGetFeedStartup))]

namespace OctopusDeployNuGetFeed.Services.NuGetFeed
{
    public class NuGetFeedStartup : IOwinStartup
    {
        private readonly IAppInsights _appInsights;
        private readonly ILogger _logger;
        private readonly ILifetimeScope _scope;

        public NuGetFeedStartup(ILogger logger, IAppInsights appInsights, ILifetimeScope scope)
        {
            _logger = logger;
            _appInsights = appInsights;
            _scope = scope;
        }

        public void Configuration(IAppBuilder app)
        {
            app.UseAutofacLifetimeScopeInjector(_scope);
            if (Environment.UserInteractive)
                app.Use(async (context, next) =>
                {
                    _logger.Verbose($"{context.Request.Method} {context.Request.Uri}");

                    await next();
                });
            if (_appInsights.IsEnabled)
                app.UseApplicationInsights(new RequestTrackingConfiguration
                {
                    ShouldTrackRequest = ShouldTrackRequest
                });

            app.UseMiddlewareFromContainer<BasicAuthentication>();

            var config = new HttpConfiguration();

            config.Services.Replace(typeof(IExceptionHandler), new PassthroughExceptionHandler(_logger));
            config.DependencyResolver = new AutofacWebApiDependencyResolver(_scope);
            config.Formatters.JsonFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/html"));

            config.Routes.MapHttpRoute(
                "HomePage",
                "",
                new {controller = "Default", action = "Index"});

            config.Routes.MapHttpRoute(
                "LoadBalancerProbe",
                "lbprobe",
                new {controller = "Default", action = "LoadBalancerProbe"});

            config.Routes.MapHttpRoute(
                "Admin",
                "admin/{action}",
                new {controller = "Admin"});

            config.Routes.MapODataServiceRoute(
                "NuGetODataFeed",
                "nuget",
                BuildNuGetODataModel(),
                new DefaultODataPathHandler(),
                GetNuGetODataConventions("NuGet"));

            config.Routes.MapHttpRoute(
                "Default",
                "{*uri}",
                new {controller = "Default", uri = RouteParameter.Optional});

            app.UseWebApi(config);
        }

        private static Task<bool> ShouldTrackRequest(IOwinContext owinContext)
        {
            return Task.FromResult(!string.Equals(owinContext.Request.Uri.AbsolutePath, "/lbprobe", StringComparison.OrdinalIgnoreCase) &&
                                   owinContext.Response.StatusCode != 401); // Avoid tracing '401 Forbidden' otherwise 50% of all requests show as failed requests
        }

        private static IEnumerable<IODataRoutingConvention> GetNuGetODataConventions(string controllerName)
        {
            var conventions = ODataRoutingConventions.CreateDefault();
            conventions.Insert(0, new MethodNameActionRoutingConvention(controllerName));
            conventions.Insert(0, new CompositeKeyRoutingConvention());

            return conventions.Select(c => new ControllerAliasingODataRoutingConvention(c, "Packages", controllerName));
        }

        private static IEdmModel BuildNuGetODataModel()
        {
            var builder = new ODataConventionModelBuilder
            {
                DataServiceVersion = new Version(2, 0),
                MaxDataServiceVersion = new Version(2, 0)
            };

            var packagesCollection = builder.EntitySet<ODataPackage>("Packages");
            packagesCollection.EntityType.HasKey(pkg => pkg.Id);
            packagesCollection.EntityType.HasKey(pkg => pkg.Version);

            var downloadPackageAction = packagesCollection.EntityType.Action("Download");

            var searchAction = builder.Action("Search");
            searchAction.Parameter<string>("searchTerm");
            searchAction.ReturnsCollectionFromEntitySet(packagesCollection);

            var findPackagesAction = builder.Action("FindPackagesById");
            findPackagesAction.Parameter<string>("id");
            findPackagesAction.ReturnsCollectionFromEntitySet(packagesCollection);

            var retValue = builder.GetEdmModel();
            retValue.SetHasDefaultStream(retValue.FindDeclaredType(typeof(ODataPackage).FullName) as IEdmEntityType, true);

            return retValue;
        }
    }
}