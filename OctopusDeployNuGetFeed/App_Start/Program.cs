using System;
using System.Fabric;
using System.Reflection;
using Autofac;
using Autofac.Integration.WebApi;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.OWIN;
using OctopusDeployNuGetFeed.ServiceFabric;
using OctopusDeployNuGetFeed.Services.NuGetFeed;
using OctopusDeployNuGetFeed.Services.ProjectRepository;
using OctopusDeployNuGetFeed.Services.ProjectRepository.Remote;
using OctopusDeployNuGetFeed.Services.ReleaseRepository;
using OctopusDeployNuGetFeed.Services.ReleaseRepository.Remote;
using OctopusDeployNuGetFeed.TopShelf;

namespace OctopusDeployNuGetFeed
{
    public class Program
    {
        public static IContainer Container { get; private set; }
        public static ILogger Logger { get; private set; }

        public static string AppInsightsInstrumentationKey { get; internal set; } = Environment.GetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(AppInsightsInstrumentationKey));

        public static string Version
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "version")
            {
                Console.Write(Version);
                return 0;
            }

            Container = BuildCompositionRoot();
            Logger = Container.Resolve<ILogger>();

            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => Logger.UnhandledException(eventArgs.ExceptionObject as Exception);

            return Container.Resolve<IProgram>().Main(args).GetAwaiter().GetResult();
        }

        private static IContainer BuildCompositionRoot()
        {
            var builder = new ContainerBuilder();

            var appInsights = string.IsNullOrWhiteSpace(AppInsightsInstrumentationKey) ? new AppInsightsNotConfigured() : (IAppInsights) new AppInsights(AppInsightsInstrumentationKey);
            appInsights.Initialize();
            builder.RegisterInstance(appInsights).As<IAppInsights>();

            builder.RegisterInstance(new LogManager(appInsights, ServiceEventSource.Current)).As<ILogger>();
            if (IsRunningOnServiceFabric())
            {
                builder.RegisterType<RemoteProjectRepositoryFactory>().As<IProjectRepositoryFactory>();
                builder.RegisterType<OctopusProjectRepositoryFactory>().AsSelf();
                builder.RegisterType<RemoteReleaseRepositoryFactory>().As<IReleaseRepositoryFactory>();
                builder.RegisterType<OctopusReleaseRepositoryFactory>().AsSelf();
                builder.RegisterType<ServiceFabricProgram>().As<IProgram>();
            }
            else
            {
                builder.RegisterType<OctopusProjectRepositoryFactory>().As<IProjectRepositoryFactory>().AsSelf();
                builder.RegisterType<OctopusReleaseRepositoryFactory>().As<IReleaseRepositoryFactory>().AsSelf();
                builder.RegisterType<TopShelfProgram>().As<IProgram>();
                builder.RegisterType<ServiceWatchdog>().AsSelf();
            }
            builder.RegisterType<NuGetFeedStartup>().As<IOwinStartup>();

            builder.RegisterType<OctopusClientFactory>().As<IOctopusClientFactory>().SingleInstance();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());

            return builder.Build();
        }

        public static bool IsRunningOnServiceFabric()
        {
            try
            {
                return FabricRuntime.GetNodeContext() != null;
            }
            catch (FabricException)
            {
                return false;
            }
        }
    }
}