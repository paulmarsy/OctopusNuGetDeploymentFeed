using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ApplicationInsights.OwinExtensions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;

namespace OctopusDeployNuGetFeed.Logging
{
    public class AppInsights : IAppInsights
    {
        private static readonly string[] PerformanceCounters =
        {
            @"\Processor(_Total)\% Processor Time",
            $"\\Process({Process.GetCurrentProcess().ProcessName})\\% Processor Time",
            $"\\Process({Process.GetCurrentProcess().ProcessName})\\Private Bytes",
            $"\\Process({Process.GetCurrentProcess().ProcessName})\\Thread Count",
            @"\.NET CLR Interop(_Global_)\# of marshalling",
            @"\.NET CLR Loading(_Global_)\% Time Loading",
            @"\.NET CLR LocksAndThreads(_Global_)\Contention Rate / sec",
            @"\.NET CLR Memory(_Global_)\# Bytes in all Heaps",
            @"\.NET CLR Remoting(_Global_)\Remote Calls/sec",
            @"\.NET CLR Jit(_Global_)\% Time in Jit",
            "\\Processor Information(_Total)\\% Processor Time",
            "\\Processor Information(_Total)\\% Privileged Time",
            "\\Processor Information(_Total)\\% User Time",
            "\\Processor Information(_Total)\\Processor Frequency",
            "\\System\\Processes",
            "\\Process(_Total)\\Thread Count",
            "\\Process(_Total)\\Handle Count",
            "\\System\\System Up Time",
            "\\System\\Context Switches/sec",
            "\\System\\Processor Queue Length",
            "\\Memory\\Available Bytes",
            "\\Memory\\Committed Bytes",
            "\\Memory\\Cache Bytes",
            "\\Memory\\Pool Paged Bytes",
            "\\Memory\\Pool Nonpaged Bytes",
            "\\Memory\\Pages/sec",
            "\\Memory\\Page Faults/sec",
            "\\Process(_Total)\\Working Set",
            "\\Process(_Total)\\Working Set - Private",
            "\\LogicalDisk(_Total)\\% Disk Time",
            "\\LogicalDisk(_Total)\\% Disk Read Time",
            "\\LogicalDisk(_Total)\\% Disk Write Time",
            "\\LogicalDisk(_Total)\\% Idle Time",
            "\\LogicalDisk(_Total)\\Disk Bytes/sec",
            "\\LogicalDisk(_Total)\\Disk Read Bytes/sec",
            "\\LogicalDisk(_Total)\\Disk Write Bytes/sec",
            "\\LogicalDisk(_Total)\\Disk Transfers/sec",
            "\\LogicalDisk(_Total)\\Disk Reads/sec",
            "\\LogicalDisk(_Total)\\Disk Writes/sec",
            "\\LogicalDisk(_Total)\\Avg. Disk sec/Transfer",
            "\\LogicalDisk(_Total)\\Avg. Disk sec/Read",
            "\\LogicalDisk(_Total)\\Avg. Disk sec/Write",
            "\\LogicalDisk(_Total)\\Avg. Disk Queue Length",
            "\\LogicalDisk(_Total)\\Avg. Disk Read Queue Length",
            "\\LogicalDisk(_Total)\\Avg. Disk Write Queue Length",
            "\\LogicalDisk(_Total)\\% Free Space",
            "\\LogicalDisk(_Total)\\Free Megabytes"
        };

        private readonly string _instrumentationKey;

        private TelemetryClient _telemetryClient;

        public AppInsights(string instrumentationKey)
        {
            _instrumentationKey = instrumentationKey;
        }

        public bool IsEnabled => true;

        public void Initialize()
        {
            if (!IsEnabled)
                return;

            TelemetryConfiguration.Active.InstrumentationKey = _instrumentationKey;

            TelemetryConfiguration.Active.TelemetryInitializers.Add(new OperationIdTelemetryInitializer());

            UseQuickPulse();

            UsePerformanceCounters();

            _telemetryClient = new TelemetryClient(TelemetryConfiguration.Active)
            {
                InstrumentationKey = _instrumentationKey
            };
            _telemetryClient.Context.Component.Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            _telemetryClient.Context.Device.Id = Environment.MachineName;
            _telemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.VersionString;
            _telemetryClient.Context.Device.Type = "Web Server";
        }

        public void TrackTrace(string message, SeverityLevel severityLevel)
        {
            _telemetryClient.TrackTrace(message, severityLevel);
        }

        public void TrackDependency(string dependencyTypeName, string target, string dependencyName, string data, DateTimeOffset startTime, TimeSpan duration, string resultCode, bool success)
        {
            _telemetryClient.TrackDependency(dependencyTypeName, target, dependencyName, data, startTime, duration, resultCode, success);
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties = null)
        {
            _telemetryClient.TrackEvent(eventName, properties);
        }

        public void TrackDependency(string dependencyName, string commandName, DateTimeOffset startTime, TimeSpan duration, bool success)
        {
            _telemetryClient.TrackDependency(dependencyName, commandName, startTime, duration, success);
        }

        public void TrackException(Exception exception, IDictionary<string, string> properties = null)
        {
            _telemetryClient.TrackException(exception, properties);
        }

        public void TrackMetric(string name, double value)
        {
            _telemetryClient.TrackMetric(name, value);
        }

        private void UseQuickPulse()
        {
            var quickPulseModule = new QuickPulseTelemetryModule();
            quickPulseModule.Initialize(TelemetryConfiguration.Active);

            TelemetryConfiguration.Active.TelemetryProcessorChainBuilder.Use(next =>
            {
                var processor = new QuickPulseTelemetryProcessor(next);
                quickPulseModule.RegisterTelemetryProcessor(processor);
                return processor;
            });
            TelemetryConfiguration.Active.TelemetryProcessorChainBuilder.Build();
        }

        private void UsePerformanceCounters()
        {
            var perfCollectorModule = new PerformanceCollectorModule();
            foreach (var counter in PerformanceCounters)
                perfCollectorModule.Counters.Add(new PerformanceCounterCollectionRequest(counter, counter.Split('\\')[1]));
            perfCollectorModule.Initialize(TelemetryConfiguration.Active);
        }
    }
}