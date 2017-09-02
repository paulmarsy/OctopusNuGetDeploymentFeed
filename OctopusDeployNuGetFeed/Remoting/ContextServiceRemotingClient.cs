using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace OctopusDeployNuGetFeed.Remoting
{
    public class ContextServiceRemotingClient : IServiceRemotingClient
    {
        private readonly Lazy<DataContractSerializer> _baggageSerializer = new Lazy<DataContractSerializer>(() => new DataContractSerializer(typeof(IEnumerable<KeyValuePair<string, string>>)));
        private readonly string _callContextDataName;
        private readonly TelemetryClient _telemetryClient = new TelemetryClient(TelemetryConfiguration.Active);

        public ContextServiceRemotingClient(IServiceRemotingClient wrappedClient, string callContextDataName)
        {
            WrappedClient = wrappedClient;
            _callContextDataName = callContextDataName;
        }

        public IServiceRemotingClient WrappedClient { get; }

        ResolvedServicePartition ICommunicationClient.ResolvedServicePartition
        {
            get => WrappedClient.ResolvedServicePartition;
            set => WrappedClient.ResolvedServicePartition = value;
        }

        string ICommunicationClient.ListenerName
        {
            get => WrappedClient.ListenerName;
            set => WrappedClient.ListenerName = value;
        }

        ResolvedServiceEndpoint ICommunicationClient.Endpoint
        {
            get => WrappedClient.Endpoint;
            set => WrappedClient.Endpoint = value;
        }

        Task<byte[]> IServiceRemotingClient.RequestResponseAsync(ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            messageHeaders.AddHeader(_callContextDataName, JsonBufferSerializer.Serialize(CallContext.LogicalGetData(_callContextDataName)));

            return SendAndTrackRequestAsync(messageHeaders, requestBody, () => WrappedClient.RequestResponseAsync(messageHeaders, requestBody));
        }

        void IServiceRemotingClient.SendOneWay(ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            messageHeaders.AddHeader(_callContextDataName, JsonBufferSerializer.Serialize(CallContext.LogicalGetData(_callContextDataName)));

            SendAndTrackRequestAsync(messageHeaders, requestBody, () =>
            {
                WrappedClient.SendOneWay(messageHeaders, requestBody);
                return Task.FromResult<byte[]>(null);
            }).Forget();
        }


        private async Task<byte[]> SendAndTrackRequestAsync(ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody, Func<Task<byte[]>> doSendRequest)
        {
            var methodName = messageHeaders.MethodId.ToString();

            // Call StartOperation, this will create a new activity with the current activity being the parent.
            // Since service remoting doesn't really have an URL like HTTP URL, we will do our best approximate that for
            // the Name, Type, Data, and Target properties
            var operation = _telemetryClient.StartOperation<DependencyTelemetry>(methodName);
            operation.Telemetry.Type = ServiceRemotingLoggingStrings.ServiceRemotingTypeName;

            try
            {
                messageHeaders.AddHeader(ServiceRemotingLoggingStrings.ParentIdHeaderName, operation.Telemetry.Id);

                // We expect the baggage to not be there at all or just contain a few small items
                var currentActivity = Activity.Current;
                if (currentActivity.Baggage.Any())
                    using (var ms = new MemoryStream())
                    {
                        var dictionaryWriter = XmlDictionaryWriter.CreateBinaryWriter(ms);
                        _baggageSerializer.Value.WriteObject(dictionaryWriter, currentActivity.Baggage);
                        dictionaryWriter.Flush();
                        messageHeaders.AddHeader(ServiceRemotingLoggingStrings.CorrelationContextHeaderName, ms.GetBuffer());
                    }

                var result = await doSendRequest().ConfigureAwait(false);
                return result;
            }
            catch (Exception e)
            {
                _telemetryClient.TrackException(e);
                operation.Telemetry.Success = false;
                throw;
            }
            finally
            {
                // Stopping the operation, this will also pop the activity created by StartOperation off the activity stack.
                _telemetryClient.StopOperation(operation);
            }
        }
    }
}