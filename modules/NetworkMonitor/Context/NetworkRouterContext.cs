using Autofac;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Context.Parameters;
using MadWizard.Desomnia.Network.Neighborhood;

namespace MadWizard.Desomnia.Network.Context
{
    public class NetworkRouterContext : NetworkHostContext
    {
        protected NetworkRouterContext(ILifetimeScope parent, AutoDiscoveryType auto) : base(parent, auto) { }

        // Router
        public NetworkRouterContext(ILifetimeScope parent, NetworkMonitorConfig configNetwork, NetworkRouterInfo config)
            : this(parent, config.AutoDetect ?? configNetwork.AutoDetect)
        {
            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                RegisterRouter<NetworkRouter>(builder, configNetwork, config);
            });
        }

        protected static void RegisterRouter<TRouter>(ContainerBuilder builder, NetworkMonitorConfig configNetwork, NetworkRouterInfo config) where TRouter : NetworkRouter
        {
            builder.RegisterType<TRouter>().As<NetworkHost>().As<NetworkRouter>()
                .WithParameter(new TypedParameter(typeof(string), config.Name))
                .WithParameter(NetworkHostsParameter.FindBy([.. config.VPNClient.Select(h => h.Name)]))
                .WithParameter(TypedParameter.From(config.MakeRouterOptions(configNetwork)))
                .OnActivated(args => ConfigureHost(args, config))
                .SingleInstance()
                .AsSelf();
        }
    }
}
