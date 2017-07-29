using System.Web.Http;
using Owin;

namespace NuGet.Server.V2.Samples.OwinHost
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            //Simple authenticator that authorizes all users that supply a username and password. Only meant for demonstration purposes.
            appBuilder.Use(typeof(BasicAuthentication));
            
            // Configure Web API for self-host. 
            var config = new HttpConfiguration();
            appBuilder.UseWebApi(config);
            
            //Map route for ordinary controllers, this is not neccessary for the NuGet feed.
            //It is just included as an example of combining ordinary controllers with NuGet OData Controllers.
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            //Feed that allows  read/download access for authenticated users, delete/upload is disabled (configured in controller's constructor).
            //User authentication is done by hosting environment, typical Owin pipeline or IIS (configured by attribute on controller).
            NuGetV2WebApiEnabler.UseNuGetV2WebApiFeed(config,
                routeName: "NuGetAdmin",
                routeUrlRoot: "NuGet/admin",
                oDatacontrollerName: "NuGetPrivateOData");            //NuGetPrivateODataController.cs, located in Controllers\ folder

            //Feed that allows unauthenticated read/download access, delete/upload requires ApiKey (configured in controller's constructor).
            NuGetV2WebApiEnabler.UseNuGetV2WebApiFeed(config,
                routeName: "NuGetPublic",
                routeUrlRoot: "NuGet/octopus",
                oDatacontrollerName: "OctopusDeployNuGetOData");            //OctopusDeployNuGetODataController.cs, located in Controllers\ folder


            //Feed that allows unauthenticated read/download/delete/upload (configured in controller's constructor).
            NuGetV2WebApiEnabler.UseNuGetV2WebApiFeed(config,
                routeName: "NuGetVeryPublic",
                routeUrlRoot: "NuGet/verypublic",
                oDatacontrollerName: "NuGetVeryPublicOData");        //NuGetVeryPublicODataController.cs, located in Controllers\ folder

        }
    }
}