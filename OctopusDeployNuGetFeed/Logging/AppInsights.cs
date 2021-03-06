﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Fabric;
using System.Fabric.Health;
using System.IO;
using ApplicationInsights.OwinExtensions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.EventSourceListener;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.ServiceFabric;

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

        public void TrackHealth(string healthMessage, HealthState state)
        {
            _telemetryClient.TrackTrace(healthMessage, GetSeverityLevel(state));
        }

        public void SetCloudContext(ServiceContext context)
        {
            _telemetryClient.Context.Cloud.RoleName = context.ServiceName.AbsoluteUri;
            _telemetryClient.Context.Cloud.RoleInstance = context.ReplicaOrInstanceId.ToString();
            _telemetryClient.Context.Device.Id = context.NodeContext.IPAddressOrFQDN;
            _telemetryClient.Context.Device.Type = context.NodeContext.NodeType;
        }


        public void TrackMetric(string name, int count, double sum, double min, double max, double standardDeviation)
        {
            var mt = new MetricTelemetry(name, count, sum, min, max, standardDeviation);

            _telemetryClient.TrackMetric(mt);
        }

        public void Critical(string message)
        {
            TrackTrace(message, SeverityLevel.Critical);
        }

        public void Error(string message)
        {
            TrackTrace(message, SeverityLevel.Error);
        }

        public void Warning(string message)
        {
            TrackTrace(message, SeverityLevel.Warning);
        }

        public void Verbose(string message)
        {
        }

        public void Info(string message)
        {
            TrackTrace(message, SeverityLevel.Information);
        }

        public void Exception(Exception exception, string callerFilePath = null, string callerMemberName = null)
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);

            TrackException(exception, new Dictionary<string, string>
            {
                {"UnhandledException", "False"},
                {"CallerFilePath", callerFilePath},
                {"CallerMemberName", callerMemberName},
                {"CallerTypeName", callerTypeName}
            });
        }

        public void UnhandledException(Exception exception)
        {
            TrackException(exception, new Dictionary<string, string>
            {
                {"UnhandledException", "True"}
            });
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

            UseEventSourceTelemetry();

            if (Program.IsRunningOnServiceFabric())
                UseFabricTelemetry();

            TelemetryConfiguration.Active.TelemetryProcessorChainBuilder.Build();

            _telemetryClient = new TelemetryClient(TelemetryConfiguration.Active)
            {
                InstrumentationKey = _instrumentationKey
            };
            _telemetryClient.Context.Component.Version = VersionProgram.Version;
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

        public void TrackException(Exception exception, IDictionary<string, string> properties = null)
        {
            _telemetryClient.TrackException(exception, properties);
        }

        public void TrackMetric(string name, double value)
        {
            _telemetryClient.TrackMetric(name, value);
        }

        public void TrackAvailability(string name, DateTimeOffset timeStamp, TimeSpan duration, string runLocation, bool success, string message = null)
        {
            _telemetryClient.TrackAvailability(name, timeStamp, duration, runLocation, success, message);
        }

        private static SeverityLevel GetSeverityLevel(HealthState state)
        {
            switch (state)
            {
                case HealthState.Warning:
                case HealthState.Invalid:
                case HealthState.Unknown:
                    return SeverityLevel.Warning;
                case HealthState.Ok:
                    return SeverityLevel.Information;
                case HealthState.Error:
                    return SeverityLevel.Error;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void UseFabricTelemetry()
        {
            TelemetryConfiguration.Active.TelemetryInitializers.Add(new FabricTelemetryInitializer());
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
        }

        private void UsePerformanceCounters()
        {
            var perfCollectorModule = new PerformanceCollectorModule();
            foreach (var counter in PerformanceCounters)
                perfCollectorModule.Counters.Add(new PerformanceCounterCollectionRequest(counter, counter.Split('\\')[1]));
            perfCollectorModule.Initialize(TelemetryConfiguration.Active);
        }

        private void UseEventSourceTelemetry()
        {
            var eventSourceModule = new EventSourceTelemetryModule();
            eventSourceModule.Sources.Add(new EventSourceListeningRequest
            {
                Name = EventSource.GetName(typeof(ServiceFabricEventSource)),
                Level = EventLevel.Informational
            });
            eventSourceModule.Initialize(TelemetryConfiguration.Active);
        }
    }
}