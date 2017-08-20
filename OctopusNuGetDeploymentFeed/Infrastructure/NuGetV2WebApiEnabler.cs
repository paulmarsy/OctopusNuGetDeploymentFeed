using System;
using System.Linq;
using System.Web.Http;
using System.Web.Http.OData.Builder;
using System.Web.Http.OData.Extensions;
using System.Web.Http.OData.Routing.Conventions;
using Microsoft.Data.Edm;
using Microsoft.Data.OData;
using OctopusDeployNuGetFeed.OData;
using OctopusDeployNuGetFeed.OData.Conventions;
using OctopusDeployNuGetFeed.OData.Routing;

namespace OctopusDeployNuGetFeed.Infrastructure
{
    public static class NuGetV2WebApiEnabler
    {
        public static HttpConfiguration UseNuGetV2WebApiFeed(this HttpConfiguration config, string routeName, string routeUrlRoot, string oDatacontrollerName)
        {
            // Insert conventions to make NuGet-compatible OData feed possible
            var conventions = ODataRoutingConventions.CreateDefault();
            conventions.Insert(0, new EntitySetCountRoutingConvention());
            conventions.Insert(0, new ActionCountRoutingConvention(oDatacontrollerName));
            conventions.Insert(0, new MethodNameActionRoutingConvention(oDatacontrollerName));
            conventions.Insert(0, new EntitySetPropertyRoutingConvention(oDatacontrollerName));
            conventions.Insert(0, new CompositeKeyRoutingConvention());

            // Translate all requests to Packages to use specified controllername instead of PackagesController
            conventions = conventions.Select(c => new ControllerAliasingODataRoutingConvention(c, "Packages", oDatacontrollerName))
                .Cast<IODataRoutingConvention>()
                .ToList();

            var oDataModel = BuildNuGetODataModel();

            config.Routes.MapODataServiceRoute(routeName, routeUrlRoot, oDataModel, new CountODataPathHandler(), conventions);
            return config;
        }


        internal static IEdmModel BuildNuGetODataModel()
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
            searchAction.Parameter<bool>("includePrerelease");
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