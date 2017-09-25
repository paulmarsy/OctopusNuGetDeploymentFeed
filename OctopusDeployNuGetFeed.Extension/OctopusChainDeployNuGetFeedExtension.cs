using Autofac;
using Autofac.Integration.WebApi;
using Octopus.Core.DomainEvents;
using Octopus.Server.Extensibility.Extensions;
using OctopusDeployNuGetFeed.Controllers;
using OctopusDeployNuGetFeed.Extension.Registration;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.OWIN;
using OctopusDeployNuGetFeed.Services.ControlService;
using OctopusDeployNuGetFeed.Services.NuGetFeed;
using OctopusDeployNuGetFeed.Services.ProjectRepository;
using OctopusDeployNuGetFeed.Services.ReleaseRepository;

namespace OctopusDeployNuGetFeed.Extension
{
    [OctopusPlugin("Octopus Chain Deploy NuGet Feed", "Paul Marston")]
    public class OctopusChainDeployNuGetFeedExtension : IOctopusExtension
    {
        static OctopusChainDeployNuGetFeedExtension()
        {
            CosturaUtility.Initialize();
        }

        public void Load(ContainerBuilder builder)
        {
            var appInsights = new AppInsightsNotConfigured();
            builder.RegisterInstance(appInsights).As<IAppInsights>();
            builder.RegisterInstance(new LogManager(appInsights, ServiceFabricEventSource.Current)).As<ILogger>();

            builder.RegisterType<OctopusProjectRepositoryFactory>().As<IProjectRepositoryFactory>().AsSelf();
            builder.RegisterType<OctopusReleaseRepositoryFactory>().As<IReleaseRepositoryFactory>().AsSelf();
            builder.RegisterType<OctopusClientFactory>().As<IOctopusClientFactory>().As<IServiceControl>();

            builder.RegisterType<BasicAuthentication>().AsSelf();
            builder.RegisterType<NuGetFeedStartup>().As<IOwinStartup>();

            builder.RegisterApiControllers(typeof(NuGetController).Assembly);


            builder.RegisterType<OctopusExtensionApiKeyService>().As<IOctopusExtensionApiKeyService>();
            builder.RegisterType<FeedRegistration>().As<IFeedRegistration>();
            builder.RegisterType<OctopusServerStateEventListener>().As<IObserveDomainEvents>();
        }
    }
}