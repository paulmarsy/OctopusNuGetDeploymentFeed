using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading.Tasks;

namespace OctopusDeployNuGetFeed.Logging
{
    [EventSource(Name = nameof(OctopusDeployNuGetFeed))]
    public sealed class ServiceFabricEventSource : EventSource, ILogger
    {
        // Define an instance method for each event you want to record and apply an [Event] attribute to it.
        // The method name is the name of the event.
        // Pass any parameters you want to record with the event (only primitive integer types, DateTime, Guid & string are allowed).
        // Each event method implementation should check whether the event source is enabled, and if it is, call WriteEvent() method to raise the event.
        // The number and types of arguments passed to every event method must exactly match what is passed to WriteEvent().
        // Put [NonEvent] attribute on all methods that do not define an event.
        // For more information see https://msdn.microsoft.com/en-us/library/system.diagnostics.tracing.eventsource.aspx

        private const int CriticalMessageEventId = 14;

        private const int ErrorMessageEventId = 13;

        private const int WarningMessageEventId = 12;

        private const int VerboseMessageEventId = 10;

        private const int InfoMessageEventId = 11;

        private const int ExceptionEventId = 15;

        private const int ServiceTypeRegisteredEventId = 3;

        private const int ServiceHostInitializationFailedEventId = 4;

        private const int ActorTypeRegisteredEventId = 5;
        public static readonly ServiceFabricEventSource Current = new ServiceFabricEventSource();

        static ServiceFabricEventSource()
        {
            // A workaround for the problem where ETW activities do not get tracked until Tasks infrastructure is initialized.
            // This problem will be fixed in .NET Framework 4.6.2.
            Task.Run(() => { });
        }

        // Instance constructor is private to enforce singleton semantics
        private ServiceFabricEventSource()
        {
        }

        [Event(CriticalMessageEventId, Level = EventLevel.Critical, Message = "{0}")]
        public void Critical(string message)
        {
            if (IsEnabled())
                WriteEvent(CriticalMessageEventId, message);
        }

        [Event(ErrorMessageEventId, Level = EventLevel.Error, Message = "{0}")]
        public void Error(string message)
        {
            if (IsEnabled())
                WriteEvent(ErrorMessageEventId, message);
        }

        [Event(WarningMessageEventId, Level = EventLevel.Warning, Message = "{0}")]
        public void Warning(string message)
        {
            if (IsEnabled())
                WriteEvent(WarningMessageEventId, message);
        }

        [Event(VerboseMessageEventId, Level = EventLevel.Verbose, Message = "{0}")]
        public void Verbose(string message)
        {
            if (IsEnabled())
                WriteEvent(VerboseMessageEventId, message);
        }

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

        [Event(ExceptionEventId, Level = EventLevel.Critical, Message = "{0}")]
        public void Exception(string exception)
        {
            if (IsEnabled())
                WriteEvent(ExceptionEventId, exception);
        }

        [Event(ServiceTypeRegisteredEventId, Level = EventLevel.Informational, Message = "Service host process {0} registered service type {1}", Keywords = Keywords.ServiceInitialization)]
        public void ServiceTypeRegistered(int hostProcessId, string serviceType)
        {
            WriteEvent(ServiceTypeRegisteredEventId, hostProcessId, serviceType);
        }

        [Event(ServiceHostInitializationFailedEventId, Level = EventLevel.Error, Message = "Service host initialization failed", Keywords = Keywords.ServiceInitialization)]
        public void ServiceHostInitializationFailed(string exception)
        {
            WriteEvent(ServiceHostInitializationFailedEventId, exception);
        }

        [Event(ActorTypeRegisteredEventId, Level = EventLevel.Informational, Message = "Service host process {0} registered actor type {1}", Keywords = Keywords.ServiceInitialization)]
        public void ActorTypeRegistered(int hostProcessId, string actorType)
        {
            WriteEvent(ServiceTypeRegisteredEventId, hostProcessId, actorType);
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
    }
}