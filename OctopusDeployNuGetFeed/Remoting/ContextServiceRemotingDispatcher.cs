using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;

namespace OctopusDeployNuGetFeed.Remoting
{
    public class ContextServiceRemotingDispatcher<TContext> : ServiceRemotingDispatcher
    {
        private readonly Lazy<DataContractSerializer> _baggageSerializer = new Lazy<DataContractSerializer>(() => new DataContractSerializer(typeof(IEnumerable<KeyValuePair<string, string>>)));
        private readonly string _callContextDataName;
        private readonly Action<TContext> _contextObjectSetter;
        private readonly TelemetryClient _telemetryClient = new TelemetryClient();


        public ContextServiceRemotingDispatcher(ServiceContext serviceContext, IService service, string callContextDataName, Action<TContext> contextObjectSetter) : base(serviceContext, service)
        {
            _callContextDataName = callContextDataName;
            _contextObjectSetter = contextObjectSetter;
        }

        public override void HandleOneWay(IServiceRemotingRequestContext requestContext, ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            if (messageHeaders.TryGetHeaderValue(_callContextDataName, out byte[] buffer))
                _contextObjectSetter(JsonBufferSerializer.Deserialize<TContext>(buffer));

            HandleAndTrackRequestAsync(messageHeaders, () =>
            {
                base.HandleOneWay(requestContext, messageHeaders, requestBody);
                return Task.FromResult<byte[]>(null);
            }).GetAwaiter().GetResult();
        }

        public override Task<byte[]> RequestResponseAsync(IServiceRemotingRequestContext requestContext, ServiceRemotingMessageHeaders messageHeaders, byte[] requestBody)
        {
            if (messageHeaders.TryGetHeaderValue(_callContextDataName, out byte[] buffer))
                _contextObjectSetter(JsonBufferSerializer.Deserialize<TContext>(buffer));

            return HandleAndTrackRequestAsync(messageHeaders, () => base.RequestResponseAsync(requestContext, messageHeaders, requestBody));
        }

        private async Task<byte[]> HandleAndTrackRequestAsync(ServiceRemotingMessageHeaders messageHeaders, Func<Task<byte[]>> doHandleRequest)
        {
            // Create and prepare activity and RequestTelemetry objects to track this request.
            var rt = new RequestTelemetry();

            if (messageHeaders.TryGetHeaderValue(ServiceRemotingLoggingStrings.ParentIdHeaderName, out string parentId))
            {
                rt.Context.Operation.ParentId = parentId;
                rt.Context.Operation.Id = GetOperationId(parentId);
            }

            // Weird case, just use the numerical id as the method name
            var methodName = messageHeaders.MethodId.ToString();
            rt.Name = methodName;

            if (messageHeaders.TryGetHeaderValue(ServiceRemotingLoggingStrings.CorrelationContextHeaderName, out byte[] correlationBytes))
            {
                var baggageBytesStream = new MemoryStream(correlationBytes, false);
                var dictionaryReader = XmlDictionaryReader.CreateBinaryReader(baggageBytesStream, XmlDictionaryReaderQuotas.Max);
                var baggage = _baggageSerializer.Value.ReadObject(dictionaryReader) as IEnumerable<KeyValuePair<string, string>>;
                foreach (var pair in baggage)
                    rt.Context.Properties.Add(pair.Key, pair.Value);
            }

            // Call StartOperation, this will create a new activity with the current activity being the parent.
            // Since service remoting doesn't really have an URL like HTTP URL, we will do our best approximate that for
            // the Name, Type, Data, and Target properties
            var operation = _telemetryClient.StartOperation(rt);

            try
            {
                var result = await doHandleRequest().ConfigureAwait(false);
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

        /// <summary>
        ///     Gets the operation Id from the request Id: substring between '|' and first '.'.
        /// </summary>
        /// <param name="id">Id to get the operation id from.</param>
        private static string GetOperationId(string id)
        {
            // id MAY start with '|' and contain '.'. We return substring between them
            // ParentId MAY NOT have hierarchical structure and we don't know if initially rootId was started with '|',
            // so we must NOT include first '|' to allow mixed hierarchical and non-hierarchical request id scenarios
            var rootEnd = id.IndexOf('.');
            if (rootEnd < 0)
                rootEnd = id.Length;

            var rootStart = id[0] == '|' ? 1 : 0;
            return id.Substring(rootStart, rootEnd - rootStart);
        }
    }
}