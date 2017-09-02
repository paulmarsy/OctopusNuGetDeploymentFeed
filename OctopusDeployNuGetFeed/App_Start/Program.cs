using System;
using System.Fabric;
using System.Reflection;
using Autofac;
using Autofac.Integration.WebApi;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.OWIN;
using OctopusDeployNuGetFeed.ServiceFabric;
using OctopusDeployNuGetFeed.Services.AdminActor.Fabric;
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


        private static int Main(string[] args)
        {
            Container = BuildCompositionRoot(args);
            Logger = Container.Resolve<ILogger>();

            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => Logger.UnhandledException(eventArgs.ExceptionObject as Exception);

            return Container.Resolve<IProgram>().Main(args).GetAwaiter().GetResult();
        }

        private static IContainer BuildCompositionRoot(string[] args)
        {
            var builder = new ContainerBuilder();

            SetProgramEntryPoint(builder, args);

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
                builder.RegisterType<RemoteAdminServiceClient>().As<IAdminService>();
            }
            else
            {
                builder.RegisterType<OctopusProjectRepositoryFactory>().As<IProjectRepositoryFactory>().AsSelf();
                builder.RegisterType<OctopusReleaseRepositoryFactory>().As<IReleaseRepositoryFactory>().AsSelf();
                builder.RegisterType<AdminService>().As<IAdminService>();
                builder.RegisterType<ServiceWatchdog>().AsSelf();
            }
            builder.RegisterType<NuGetFeedStartup>().As<IOwinStartup>();

            builder.RegisterType<OctopusClientFactory>().As<IOctopusClientFactory>().SingleInstance();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());

            return builder.Build();
        }

        private static void SetProgramEntryPoint(ContainerBuilder builder, string[] args)
        {
            var entryPoint = typeof(TopShelfProgram);
            ;
            if (IsRunningOnServiceFabric())
                entryPoint = typeof(ServiceFabricProgram);

            if (args.Length >= 1)
            {
                if (args[0] == "version")
                    entryPoint = typeof(VersionProgram);
                if (args[0] == "deploy-service-fabric")
                    entryPoint = typeof(ServiceFabricDeploy);
            }

            builder.RegisterType(entryPoint).As<IProgram>();
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