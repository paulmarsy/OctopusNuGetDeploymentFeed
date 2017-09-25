using System;
using Microsoft.Owin.Hosting;
using Octopus.Configuration;
using Octopus.Core.DomainEvents;
using Octopus.Core.DomainEvents.Events;
using Octopus.Diagnostics;
using OctopusDeployNuGetFeed.Extension.Registration;
using OctopusDeployNuGetFeed.OWIN;

namespace OctopusDeployNuGetFeed.Extension
{
    public class OctopusServerStateEventListener : IObserveDomainEvents
    {
        public const string PortKey = "OctopusDeployNuGetFeed.Extension.Port";
        private readonly IKeyValueStore _configStore;
        private readonly IFeedRegistration _feedRegistration;
        private readonly ILog _log;
        private readonly IOwinStartup _startup;
        private IDisposable _webApiApp;

        public OctopusServerStateEventListener(IOwinStartup startup, IFeedRegistration feedRegistration, IKeyValueStore configStore, ILog log)
        {
            _startup = startup;
            _feedRegistration = feedRegistration;
            _configStore = configStore;
            _log = log;
        }

        public void Raise(DomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case ServerStartedEvent _:
                    ServerStartedEvent();
                    break;
                case ServerStoppedEvent _:
                    ServerStoppedEvent();
                    break;
            }
        }

        public void ServerStartedEvent()
        {
            try
            {
                var port = _configStore.Get(PortKey);
                if (port == null)
                {
                    port = new Random().Next(10000, 65535).ToString();
                    _configStore.Set(PortKey, port);
                }
                var listenPrefix = $"http://localhost:{port}/";
                _feedRegistration.Register(listenPrefix);

                _webApiApp = WebApp.Start(listenPrefix, _startup.Configuration);
            }
            catch (Exception e)
            {
                _log.Fatal(e);
                throw;
            }
        }

        public void ServerStoppedEvent()
        {
            _webApiApp.Dispose();
        }
    }
}