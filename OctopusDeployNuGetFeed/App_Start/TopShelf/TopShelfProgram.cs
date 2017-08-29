using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using OctopusDeployNuGetFeed.Logging;
using OctopusDeployNuGetFeed.OWIN;
using Topshelf;

namespace OctopusDeployNuGetFeed.TopShelf
{
    public class TopShelfProgram : IProgram
    {
        private readonly ILogger _logger;
        private readonly IOwinStartup _startup;
        private readonly ServiceWatchdog _watchdog;
        private IDisposable _webApiApp;

        public TopShelfProgram(ILogger logger, ServiceWatchdog watchdog, IOwinStartup startup)
        {
            _logger = logger;
            _watchdog = watchdog;
            _startup = startup;
        }

        private string Host { get; set; } = Environment.GetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(Host)) ?? "+";
        private string Port { get; set; } = Environment.GetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(Port)) ?? "80";
        private string ListeningAddress => $"http://{Host}:{Port}/";


        public Task<int> Main(string[] args)
        {
            return Task.FromResult((int) Run(args));
        }

        private TopshelfExitCode Run(string[] args)
        {
            if (args.Length == 1 && args[0] == ServiceWatchdog.ArgName)
            {
                _watchdog.Check();
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
                c.AddCommandLineDefinition("aikey", aikey => Program.AppInsightsInstrumentationKey = aikey);

                c.Service<IOwinStartup>(s =>
                {
                    s.ConstructUsing(() => _startup);
                    s.WhenStarted(service => _webApiApp = WebApp.Start(ListeningAddress, service.Configuration));
                    s.WhenStopped(service => _webApiApp.Dispose());
                });
            });
        }

        private void BeforeInstall()
        {
            _logger.Info($"Setting Host: {Host}");
            Environment.SetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(Host), Host, EnvironmentVariableTarget.Machine);

            _logger.Info($"Setting Port: {Port}");
            Environment.SetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(Port), Port, EnvironmentVariableTarget.Machine);

            _logger.Info($"Setting Application Insights Instrumentation Key: {Program.AppInsightsInstrumentationKey}");
            Environment.SetEnvironmentVariable(nameof(OctopusDeployNuGetFeed) + nameof(Program.AppInsightsInstrumentationKey), Program.AppInsightsInstrumentationKey, EnvironmentVariableTarget.Machine);

            _logger.Info($"Adding URL reservation for {ListeningAddress}...");
            StartProcess("netsh.exe", $"http delete urlacl url={ListeningAddress}", false);
            StartProcess("netsh.exe", $"http add urlacl url={ListeningAddress} user=\"NETWORK SERVICE\"");

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
            StartProcess("netsh.exe", $"http delete urlacl url={ListeningAddress}");
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