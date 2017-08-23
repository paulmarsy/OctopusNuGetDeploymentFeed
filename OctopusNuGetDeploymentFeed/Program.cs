using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Autofac;
using Autofac.Integration.WebApi;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.Octopus;
using Topshelf;

namespace OctopusDeployNuGetFeed
{
    public class Program
    {
        private readonly ILogger _logger;
        private readonly Watchdog _watchdog;

        public Program(ILogger logger, Watchdog watchdog)
        {
            _logger = logger;
            _watchdog = watchdog;
        }

        public static IContainer Container { get; private set; }
        public static string Host { get; private set; } = Environment.GetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(Host)) ?? "+";
        public static string Port { get; private set; } = Environment.GetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(Port)) ?? "80";
        public static string BaseAddress => $"http://{Host}:{Port}/";
        public static string AppInsightsInstrumentationKey { get; private set; } = Environment.GetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(AppInsightsInstrumentationKey));

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
            var builder = new ContainerBuilder();

            var appInsights = string.IsNullOrWhiteSpace(AppInsightsInstrumentationKey) ? new AppInsightsNotConfigured() : (IAppInsights) new AppInsights(AppInsightsInstrumentationKey);
            appInsights.Initialize();
            builder.RegisterInstance(appInsights).As<IAppInsights>();

            builder.RegisterInstance(new LogManager(appInsights)).As<ILogger>();

            builder.RegisterType<Program>().AsSelf();
            builder.RegisterType<Watchdog>().AsSelf();
            builder.RegisterType<Startup>().AsSelf();

            builder.RegisterType<OctopusPackageRepositoryFactory>().As<IPackageRepositoryFactory>().SingleInstance();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());

            Container = builder.Build();
            return (int) Container.Resolve<Program>().Run(args);
        }

        private TopshelfExitCode Run(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => _logger.UnhandledException(eventArgs.ExceptionObject as Exception);

            if (args.Length == 1 && args[0] == Watchdog.ArgName)
            {
                _watchdog.Check();
                return 0;
            }
            if (args.Length == 1 && args[0] == "version")
            {
                Console.Write(Version);
                return 0;
            }
            return HostFactory.Run(c =>
            {
                c.SetDescription("Octopus Deploy NuGet Deployment Feed");
                c.SetDisplayName(nameof(OctopusDeployNuGetFeed));
                c.SetServiceName(nameof(OctopusDeployNuGetFeed));
                c.RunAsNetworkService();
                c.StartAutomatically();

                c.BeforeInstall(BeforeInstall);
                c.AfterInstall(AfterInstall);

                c.BeforeUninstall(BeforeUninstall);
                c.AfterUninstall(AfterUninstall);

                c.OnException(_logger.UnhandledException);
                c.AddCommandLineDefinition("host", host => Host = host);
                c.AddCommandLineDefinition("port", port => Port = port);
                c.AddCommandLineDefinition("aikey", aikey => AppInsightsInstrumentationKey = aikey);

                c.Service<Startup>(s =>
                {
                    s.ConstructUsing(() => Container.Resolve<Startup>());
                    s.WhenStarted(service => service.Start(_logger));
                    s.WhenStopped(service => service.Stop());
                });
            });
        }

        private void BeforeInstall()
        {
            _logger.Info($"Setting Host: {Host}");
            Environment.SetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(Host), Host, EnvironmentVariableTarget.Machine);

            _logger.Info($"Setting Port: {Port}");
            Environment.SetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(Port), Port, EnvironmentVariableTarget.Machine);

            _logger.Info($"Setting Application Insights Instrumentation Key: {AppInsightsInstrumentationKey}");
            Environment.SetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(AppInsightsInstrumentationKey), AppInsightsInstrumentationKey, EnvironmentVariableTarget.Machine);

            _logger.Info($"Adding URL reservation for {BaseAddress}...");
            StartProcess("netsh.exe", $"http delete urlacl url={BaseAddress}", false);
            StartProcess("netsh.exe", $"http add urlacl url={BaseAddress} user=\"NETWORK SERVICE\"");

            _logger.Info("Adding Port 80 firewall rule...");
            StartProcess("netsh.exe", "advfirewall firewall add rule name=\"HTTP\" dir=in action=allow protocol=TCP localport=80");
        }

        private void AfterInstall()
        {
            _watchdog.CreateTask();
        }

        private void BeforeUninstall()
        {
            _watchdog.DeleteTask();
        }

        private void AfterUninstall()
        {
            _logger.Info("Removing URL reservation...");
            StartProcess("netsh.exe", $"http delete urlacl url={BaseAddress}");
        }

        private static void StartProcess(string fileName, string arguments)
        {
            StartProcess(fileName, arguments, true);
        }

        private static void StartProcess(string fileName, string arguments, bool checkExitCode)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process.WaitForExit();
            if (checkExitCode && process.ExitCode != 0)
                throw new ExternalException($"Non-zero exit code from {fileName}: {process.ExitCode}");
        }
    }
}