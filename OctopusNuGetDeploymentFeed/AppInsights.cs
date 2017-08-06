using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using ApplicationInsights.OwinExtensions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using OctopusDeployNuGetFeed.Logging;

namespace OctopusDeployNuGetFeed
{
    public class AppInsights
    {
        private static readonly string[] PerformanceCounters =
        {
            @"\.NET Memory Cache 4.0(*)\Cache Hits",
            @"\.NET Memory Cache 4.0(*)\Cache Misses",
            @"\.NET Memory Cache 4.0(*)\Cache Hit Ratio",
            @"\.NET Memory Cache 4.0(*)\Cache Trims",
            @"\.NET Memory Cache 4.0(*)\Cache Entries",
            @"\.NET Memory Cache 4.0(*)\Cache Turnover Rate",
            @"\Memory\Available Bytes",
            @"\Processor(_Total)\% Processor Time",
            $"\\Process({Process.GetCurrentProcess().ProcessName})\\% Processor Time",
            $"\\Process({Process.GetCurrentProcess().ProcessName})\\Private Bytes",
            $"\\Process({Process.GetCurrentProcess().ProcessName})\\Thread Count",
            @"\.NET CLR Interop(_Global_)\# of marshalling",
            @"\.NET CLR Loading(_Global_)\% Time Loading",
            @"\.NET CLR LocksAndThreads(_Global_)\Contention Rate / sec",
            @"\.NET CLR Memory(_Global_)\# Bytes in all Heaps",
            @"\.NET CLR Networking(_Global_)\Connections Established",
            @"\.NET CLR Remoting(_Global_)\Remote Calls/sec",
            @"\.NET CLR Jit(_Global_)\% Time in Jit"
        };

        private readonly ILogger _logger = LogManager.Current;
        public bool IsEnabled => !string.IsNullOrWhiteSpace(AppInsightsKey);

        public TelemetryClient TelemetryClient { get; private set; }
        public TelemetryConfiguration TelemetryConfiguration { get; } = TelemetryConfiguration.Active;
        private static string AppInsightsKey => Environment.GetEnvironmentVariable("AppInsightsInstrumentationKey");

        public void Initialize()
        {
            if (!IsEnabled)
                return;

            _logger.Info("Configuring App Insights Telemetry...");

            TelemetryConfiguration.InstrumentationKey = AppInsightsKey;

            TelemetryConfiguration.TelemetryInitializers.Add(new OperationIdTelemetryInitializer());

            UseQuickPulse();

            UsePerformanceCounters();

            TelemetryClient = new TelemetryClient(TelemetryConfiguration)
            {
                InstrumentationKey = AppInsightsKey
            };
            TelemetryClient.Context.Component.Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            TelemetryClient.Context.Device.Id = Environment.MachineName;
            TelemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.VersionString;
            TelemetryClient.Context.Device.Type = "Web Server";
        }
        
        private void UseQuickPulse()
        {
            var quickPulseModule = new QuickPulseTelemetryModule();
            quickPulseModule.Initialize(TelemetryConfiguration);

            TelemetryConfiguration.TelemetryProcessorChainBuilder.Use(next =>
            {
                var processor = new QuickPulseTelemetryProcessor(next);
                quickPulseModule.RegisterTelemetryProcessor(processor);
                return processor;
            });
            TelemetryConfiguration.TelemetryProcessorChainBuilder.Build();
        }

        private void UsePerformanceCounters()
        {
            var perfCollectorModule = new PerformanceCollectorModule();
            foreach (var counter in PerformanceCounters)
                perfCollectorModule.Counters.Add(new PerformanceCounterCollectionRequest(counter, counter.Split('\\')[1]));
            perfCollectorModule.Initialize(TelemetryConfiguration);
        }
    }
}