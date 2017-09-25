using System;
using System.Fabric;
using System.Reflection;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.Win32;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus;
using OctopusDeployNuGetFeed.OWIN;
using OctopusDeployNuGetFeed.ServiceFabric;
using OctopusDeployNuGetFeed.Services.ControlService;
using OctopusDeployNuGetFeed.Services.ControlService.Remote;
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
        public static string AppInsightsInstrumentationKey { get; internal set; } = Environment.GetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(AppInsightsInstrumentationKey));

        public static void Main(string[] args)
        {
            var container = BuildCompositionRoot(args);

            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => container.Resolve<ILogger>().UnhandledException(eventArgs.ExceptionObject as Exception);
            ExtensionMethods.Logger = container.Resolve<ILogger>();

            container.Resolve<IProgram>().Main(args).GetAwaiter().GetResult();
        }

        private static IContainer BuildCompositionRoot(string[] args)
        {
            var builder = new ContainerBuilder();

            SetProgramEntryPoint(builder, args);

            var appInsights = string.IsNullOrWhiteSpace(AppInsightsInstrumentationKey) ? new AppInsightsNotConfigured() : (IAppInsights) new AppInsights(AppInsightsInstrumentationKey);
            appInsights.Initialize();
            builder.RegisterInstance(appInsights).As<IAppInsights>();

            var logger = new LogManager(appInsights, ServiceFabricEventSource.Current);
            builder.RegisterInstance(logger).As<ILogger>();
            var octopusClientFactory = new OctopusClientFactory(logger, appInsights);
            if (IsRunningOnServiceFabric())
            {
                builder.RegisterType<RemoteProjectRepositoryFactory>().As<IProjectRepositoryFactory>();
                builder.RegisterType<OctopusProjectRepositoryFactory>().AsSelf();
                builder.RegisterType<RemoteReleaseRepositoryFactory>().As<IReleaseRepositoryFactory>();
                builder.RegisterType<OctopusReleaseRepositoryFactory>().AsSelf();
                builder.RegisterType<RemoteServiceControlClient>().As<IServiceControl>();
                builder.RegisterInstance(octopusClientFactory).As<IOctopusClientFactory>();
            }
            else
            {
                builder.RegisterType<OctopusProjectRepositoryFactory>().As<IProjectRepositoryFactory>().AsSelf();
                builder.RegisterType<OctopusReleaseRepositoryFactory>().As<IReleaseRepositoryFactory>().AsSelf();
                builder.RegisterInstance(octopusClientFactory).As<IOctopusClientFactory>().As<IServiceControl>();

                builder.RegisterType<ServiceWatchdog>().AsSelf();
            }
            builder.RegisterType<BasicAuthentication>().AsSelf();
            builder.RegisterType<NuGetFeedStartup>().As<IOwinStartup>();

            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());

            return builder.Build();
        }

        private static void SetProgramEntryPoint(ContainerBuilder builder, string[] args)
        {
            var entryPoint = typeof(TopShelfProgram);
            if (IsRunningOnServiceFabric())
                entryPoint = typeof(ServiceFabricProgram);

            if (args.Length >= 1)
            {
                if (args[0] == VersionProgram.Parameter)
                    entryPoint = typeof(VersionProgram);
                if (args[0] == ServiceWatchdog.Parameter)
                    entryPoint = typeof(ServiceWatchdog);
                if (args[0] == ServiceFabricDeploy.Parameter)
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
            catch (TypeInitializationException)
            {
                return false;
            }
        }

        public static bool IsServiceFabricSdkKeyInRegistry()
        {
            var keyFound = false;
            using (var rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var subKey = rootKey.OpenSubKey("SOFTWARE\\Microsoft\\Service Fabric SDK", false))
            {
                if (subKey != null)
                    keyFound = true;
            }
            return keyFound;
        }
    }
}