using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed.Logging
{
    [EventSource(Name = nameof(OctopusDeployNuGetFeed))]
    public sealed class ServiceEventSource : EventSource, ILogger
    {
        public static readonly ServiceEventSource Current = new ServiceEventSource();

        static ServiceEventSource()
        {
            // A workaround for the problem where ETW activities do not get tracked until Tasks infrastructure is initialized.
            // This problem will be fixed in .NET Framework 4.6.2.
            Task.Run(() => { });
        }

        // Instance constructor is private to enforce singleton semantics
        private ServiceEventSource()
        {
        }

        #region Keywords

        // Event keywords can be used to categorize events. 
        // Each keyword is a bit flag. A single event can be associated with multiple keywords (via EventAttribute.Keywords property).
        // Keywords must be defined as a public class named 'Keywords' inside EventSource that uses them.
        public static class Keywords
        {
            public const EventKeywords Requests = (EventKeywords) 0x1L;
            public const EventKeywords ServiceInitialization = (EventKeywords) 0x2L;
            public const EventKeywords HostInitialization = (EventKeywords) 0x2L;
        }

        #endregion

        #region Events

        // Define an instance method for each event you want to record and apply an [Event] attribute to it.
        // The method name is the name of the event.
        // Pass any parameters you want to record with the event (only primitive integer types, DateTime, Guid & string are allowed).
        // Each event method implementation should check whether the event source is enabled, and if it is, call WriteEvent() method to raise the event.
        // The number and types of arguments passed to every event method must exactly match what is passed to WriteEvent().
        // Put [NonEvent] attribute on all methods that do not define an event.
        // For more information see https://msdn.microsoft.com/en-us/library/system.diagnostics.tracing.eventsource.aspx

        private const int CriticalMessageEventId = 14;

        [Event(CriticalMessageEventId, Level = EventLevel.Critical, Message = "{0}")]
        public void Critical(string message)
        {
            if (IsEnabled())
                WriteEvent(CriticalMessageEventId, message);
        }

        private const int ErrorMessageEventId = 13;

        [Event(ErrorMessageEventId, Level = EventLevel.Error, Message = "{0}")]
        public void Error(string message)
        {
            if (IsEnabled())
                WriteEvent(ErrorMessageEventId, message);
        }

        private const int WarningMessageEventId = 12;

        [Event(WarningMessageEventId, Level = EventLevel.Warning, Message = "{0}")]
        public void Warning(string message)
        {
            if (IsEnabled())
                WriteEvent(WarningMessageEventId, message);
        }

        private const int VerboseMessageEventId = 10;

        [Event(VerboseMessageEventId, Level = EventLevel.Verbose, Message = "{0}")]
        public void Verbose(string message)
        {
            if (IsEnabled())
                WriteEvent(VerboseMessageEventId, message);
        }

        private const int InfoMessageEventId = 11;

        [Event(InfoMessageEventId, Level = EventLevel.Informational, Message = "{0}")]
        public void Info(string message)
        {
            if (IsEnabled())
                WriteEvent(InfoMessageEventId, message);
        }

        [NonEvent]
        public void Exception(Exception exception, string callerFilePath = null, string callerMemberName = null)
        {
            var callerTypeName = Path.GetFileNameWithoutExtension(callerFilePath);
            Exception(exception, $"{callerTypeName}.{callerMemberName}");
        }

        [NonEvent]
        public void UnhandledException(Exception exception)
        {
            Exception(exception, "Unhandled Exception");
        }

        [NonEvent]
        private void Exception(Exception exception, string source)
        {
            Exception($"{source}: {exception.GetType().Name} {exception.Message}. {exception.InnerException?.GetType().Name} {exception.InnerException?.Message}\n{exception.StackTrace}");
        }

        private const int ExceptionEventId = 15;

        [Event(ExceptionEventId, Level = EventLevel.Critical, Message = "{0}")]
        public void Exception(string exception)
        {
            if (IsEnabled())
                WriteEvent(ExceptionEventId, exception);
        }

        private const int ServiceTypeRegisteredEventId = 3;

        [Event(ServiceTypeRegisteredEventId, Level = EventLevel.Informational, Message = "Service host process {0} registered service type {1}", Keywords = Keywords.ServiceInitialization)]
        public void ServiceTypeRegistered(int hostProcessId, string serviceType)
        {
            WriteEvent(ServiceTypeRegisteredEventId, hostProcessId, serviceType);
        }

        private const int ServiceHostInitializationFailedEventId = 4;

        [Event(ServiceHostInitializationFailedEventId, Level = EventLevel.Error, Message = "Service host initialization failed", Keywords = Keywords.ServiceInitialization)]
        public void ServiceHostInitializationFailed(string exception)
        {
            WriteEvent(ServiceHostInitializationFailedEventId, exception);
        }

        // A pair of events sharing the same name prefix with a "Start"/"Stop" suffix implicitly marks boundaries of an event tracing activity.
        // These activities can be automatically picked up by debugging and profiling tools, which can compute their execution time, child activities,
        // and other statistics.
        private const int ServiceRequestStartEventId = 5;

        [Event(ServiceRequestStartEventId, Level = EventLevel.Informational, Message = "Service request '{0}' started", Keywords = Keywords.Requests)]
        public void ServiceRequestStart(string requestTypeName)
        {
            WriteEvent(ServiceRequestStartEventId, requestTypeName);
        }

        private const int ServiceRequestStopEventId = 6;

        [Event(ServiceRequestStopEventId, Level = EventLevel.Informational, Message = "Service request '{0}' finished", Keywords = Keywords.Requests)]
        public void ServiceRequestStop(string requestTypeName, string exception = "")
        {
            WriteEvent(ServiceRequestStopEventId, requestTypeName, exception);
        }

        private const int ActorHostInitializationFailedEventId = 7;

        [Event(ActorHostInitializationFailedEventId, Level = EventLevel.Error, Message = "Actor host initialization failed", Keywords = Keywords.HostInitialization)]
        public void ActorHostInitializationFailed(string exception)
        {
            WriteEvent(ActorHostInitializationFailedEventId, exception);
        }

        #endregion

        #region Private methods

#if UNSAFE
        private int SizeInBytes(string s)
        {
            if (s == null)
            {
                return 0;
            }
            else
            {
                return (s.Length + 1) * sizeof(char);
            }
        }
#endif

        #endregion
    }
}