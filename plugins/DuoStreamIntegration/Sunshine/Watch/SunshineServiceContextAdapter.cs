using Autofac;
using MadWizard.Desomnia.Network;
using MadWizard.Desomnia.Network.Context;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Service.Duo.Manager;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Service.Duo.Sunshine.Watch
{
    internal class SunshineServiceContextAdapter(DuoManager manager) : IStartable, INetworkService, IDisposable
    {
        public required ILogger<SunshineServiceContextAdapter> Logger { get; set; }

        public required NetworkContext Context { private get; init; }

        private Dictionary<DuoInstance, SunshineServiceContext> _services = [];

        void IStartable.Start()
        {
            manager.Started += Manager_Started;
            manager.Stopped += Manager_Stopped;
        }

        private void Manager_Started(object? sender, EventArgs e)
        {
            ((INetworkService)this).Startup();
        }

        void INetworkService.Startup()
        {
            var ctxLocalHost = Context.First(ctx => ctx.Host is LocalHost);

            foreach (var instance in manager)
            {
                Logger.LogInformation($"Monitoring {instance}:{instance.Port}" + (instance.IsRunning == true ? " (running)" : ""));

                var context = ctxLocalHost.CreateService<SunshineServiceContext>(TypedParameter.From(instance.Service));

                instance.StartTracking(context.Watch);

                _services.Add(instance, context);
            }
        }

        void INetworkService.Shutdown()
        {
            foreach (var srv in _services)
            {
                srv.Key.StopTracking(srv.Value.Watch);

                srv.Value.Dispose();
            }

            _services.Clear();
        }

        private void Manager_Stopped(object? sender, EventArgs e)
        {
            ((INetworkService)this).Shutdown();
        }

        void IDisposable.Dispose()
        {
            manager.Started -= Manager_Started;
            manager.Stopped -= Manager_Stopped;
        }
    }
}