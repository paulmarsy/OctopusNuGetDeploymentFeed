using OctopusDeployNuGetFeed.Logging;
using Topshelf;

namespace OctopusDeployNuGetFeed
{
    public class Program
    {
        public static string Host { get; private set; } = "+";
        public static int Port { get; private set; } = 80;

        private static int Main(string[] args)
        {
            return (int) HostFactory.New(c =>
            {
                c.SetDescription("Octopus Deploy NuGet Deployment Feed");
                c.SetDisplayName(nameof(OctopusDeployNuGetFeed));
                c.SetServiceName(nameof(OctopusDeployNuGetFeed));
                c.RunAsNetworkService();
                c.StartAutomaticallyDelayed();

                c.EnableServiceRecovery(recoveryConfiguration =>
                {
                    recoveryConfiguration.OnCrashOnly();
                    recoveryConfiguration.RestartService(1); // First failure
                    recoveryConfiguration.RestartService(3); // Second failure
                    recoveryConfiguration.RestartService(5); // Subsequent failures
                    recoveryConfiguration.SetResetPeriod(1);
                });

                c.AddCommandLineDefinition("host", host => Host = host);
                c.AddCommandLineDefinition("port", port => Port = int.Parse(port));

                c.Service<Startup>(s =>
                {
                    s.ConstructUsing(() => new Startup());
                    s.WhenStarted(service => service.Start());
                    s.WhenStopped(service => service.Stop());
                });

                c.OnException(e => { LogManager.Current.Error($"Unhandled Exception!!: {e?.Message}. {e?.InnerException?.Message}\n{e?.StackTrace}"); });
            }).Run();
        }
    }
}