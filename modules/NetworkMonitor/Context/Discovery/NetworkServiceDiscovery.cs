using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Configuration.Services;
using MadWizard.Desomnia.Network.Discovery;
using MadWizard.Desomnia.Network.Discovery.BuiltIn;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Context
{
    public partial class NetworkContext
    {
        private static void RegisterServiceDiscovery(ContainerBuilder builder, NetworkMonitorConfig config)
        {
            if (config.AutoDetect.HasFlag(AutoDiscoveryType.SleepProxy))
                builder.RegisterType<SleepProxyDetector>()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

            builder.RegisterType<RemoteHostServiceDetector>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();
        }

        internal async Task DiscoverServices()
        {
            Logger.LogDebug("Discovering services...");

            if (Scope.ResolveOptional<IServiceDiscovery>() is IServiceDiscovery discovery)
            {
                await discovery.DiscoverServices(Network);
            }

            foreach (var ctx in _hostContexts.Where(ctx => ctx.Auto.HasFlag(AutoDiscoveryType.Service)))
            {
                if (ctx.Watch is not null && Scope.ResolveOptional<IWatchedServiceDiscovery>() is IWatchedServiceDiscovery discoveryHost)
                {
                    await discoveryHost.DiscoverServices(ctx.Watch);
                }
            }
        }
    }

    public partial class NetworkHostContext
    {
        internal void CreateStaticWatchedServices(IEnumerable<WatchedServiceInfo> services)
        {
            foreach (var info in services)
            {
                CreateWatchedService(info, new(ServiceFlags.Static));
            }
        }

        public NetworkServiceContext CreateWatchedService(WatchedServiceInfo info, ServiceOptions options = default)
        {
            return CreateWatchedService<NetworkServiceContext>(
                new TypedParameter(typeof(WatchedServiceInfo), info), 
                new TypedParameter(typeof(ServiceOptions), options));
        }

        public T CreateWatchedService<T>(params Parameter[] parameters) where T : NetworkServiceContext
        {
            var ctx = Scope.Resolve<T>(parameters);

            try
            {
                Host.AddService(ctx.Service, ctx.Options);

                Logger.LogHostServiceAdded(Host, ctx.Service);
            }
            catch (Exception) // service probably already exists
            {
                ctx.Dispose();

                throw;
            }

            Watch?.StartTracking(ctx.Watch);

            ctx.Scope.CurrentScopeEnding += (sender, args) =>
            {
                Host.RemoveService(ctx.Service);

                Logger.LogHostServiceRemoved(Host, ctx.Service);

                Watch?.StopTracking(ctx.Watch);

                _serviceContexts.Remove(ctx);
            };

            _serviceContexts.Add(ctx);

            return ctx;
        }
    }
}
