using System;
using System.Diagnostics;
using System.Reflection;
using OctopusDeployNuGetFeed.Logging;
using Topshelf;

namespace OctopusDeployNuGetFeed
{
    public class Program
    {
        public static string Host { get; private set; } = "+";
        public static int Port { get; private set; } = 80;
        public static string BaseAddress => $"http://{Host}:{Port}/";
        public static string AppInsightsKey => Environment.GetEnvironmentVariable("AppInsightsInstrumentationKey");

        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => LogManager.Current.UnhandledException(eventArgs.ExceptionObject as Exception);

            var watchdog = new Watchdog(LogManager.Current);
            if (args.Length == 1 && args[0] == Watchdog.ArgName)
            {
                watchdog.Check();
                return 0;
            }
            if (args.Length == 1 && args[0] == "version")
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                Console.Write($"{version.Major}.{version.Minor}.{version.Build}");
                return 0;
            }
            return (int) HostFactory.New(c =>
            {
                c.SetDescription("Octopus Deploy NuGet Deployment Feed");
                c.SetDisplayName(nameof(OctopusDeployNuGetFeed));
                c.SetServiceName(nameof(OctopusDeployNuGetFeed));
                c.RunAsNetworkService();
                c.StartAutomatically();

                c.OnException(LogManager.Current.UnhandledException);
                c.AddCommandLineDefinition("host", host => Host = host);
                c.AddCommandLineDefinition("port", port => Port = int.Parse(port));

                c.Service<Startup>(s =>
                {
                    s.ConstructUsing(() => new Startup());
                    s.WhenStarted(service => service.Start());
                    s.WhenStopped(service => service.Stop());
                });
                c.BeforeInstall(() => Process.Start("netsh.exe", $"http add urlacl url={BaseAddress} user=\"NETWORK SERVICE\""));
                c.AfterUninstall(() => Process.Start("netsh.exe", $"http delete urlacl url={BaseAddress}"));
#if !DEBUG
                c.AfterInstall(() => watchdog.CreateTask());
                c.BeforeUninstall(() => watchdog.DeleteTask());
#endif
            }).Run();
        }
    }
}