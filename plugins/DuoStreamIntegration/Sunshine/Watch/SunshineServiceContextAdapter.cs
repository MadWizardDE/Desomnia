using Autofac;
using MadWizard.Desomnia.Network;
using MadWizard.Desomnia.Network.Context;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Service.Duo.Manager;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Service.Duo.Sunshine.Watch
{
    internal class SunshineServiceContextAdapter(DuoManager manager) : INetworkService
    {
        public required ILogger<SunshineServiceContextAdapter> Logger { get; set; }

        public required NetworkContext Context { private get; init; }

        private Dictionary<DuoInstance, SunshineServiceContext> _services = [];

        void INetworkService.Startup()
        {
            WatchInstances();

            manager.Started += WatchInstances;
            manager.Stopped += UnWatchInstaces;
        }

        private void WatchInstances(object? sender = null, EventArgs? e = null)
        {
            var ctxLocalHost = Context.First(ctx => ctx.Host is LocalHost);

            foreach (var instance in manager) using (Context.Network.Mutex.Lock())
            {
                try
                {
                    Logger.LogInformation($"Monitoring {instance}:{instance.Port}" + (instance.IsRunning == true ? " (running)" : ""));

                    var context = ctxLocalHost.CreateWatchedService<SunshineServiceContext>(TypedParameter.From(instance.Service));

                    instance.StartTracking(context.Watch);

                    _services.Add(instance, context);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"NOT Monitoring {instance}:{instance.Port} -> could not create service context");
                }
            }
        }

        private void UnWatchInstaces(object? sender = null, EventArgs? e = null)
        {
            foreach (var srv in _services) using (Context.Network.Mutex.Lock())
            {
                srv.Key.StopTracking(srv.Value.Watch);

                srv.Value.Dispose();
            }

            _services.Clear();
        }

        void INetworkService.Shutdown()
        {
            manager.Stopped -= UnWatchInstaces;
            manager.Started -= WatchInstances;

            UnWatchInstaces();
        }
    }
}