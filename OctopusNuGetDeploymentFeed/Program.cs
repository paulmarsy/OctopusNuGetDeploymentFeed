using Topshelf;

namespace OctopusDeployNuGetFeed
{
    public class Program
    {
        public static string Host { get; private set; } = "localhost";
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

                c.AddCommandLineDefinition("host", host => Host = host);
                c.AddCommandLineDefinition("port", port => Port = int.Parse(port));

                c.Service<Startup>(s =>
                {
                    s.ConstructUsing(() => new Startup());
                    s.WhenStarted(service => service.Start());
                    s.WhenStopped(service => service.Stop());
                });
            }).Run();
        }
    }
}