using Autofac;
using MadWizard.Desomnia.Network.Traefik.Configuration;
using MadWizard.Desomnia.Network.Traefik.Filter;

namespace MadWizard.Desomnia.Network.Traefik
{
    public class PluginModule : Desomnia.ConfigurableModule<ModuleConfig>
    {
        protected override void Load(ContainerBuilder builder)
        {
            foreach (var network in Config.NetworkMonitor)
            {
                builder.RegisterType<NetworkPluginModule>().As<Desomnia.Network.PluginModule>()
                    .WithMetadata<Network.PluginModule.Metadata>(meta => meta.For(m => m.Name, network.Name))
                    .WithParameter(TypedParameter.From(network))
                    .SingleInstance();
            }

            builder.RegisterComposite<CompositeTraefikRequestFilter, ITraefikRequestFilter>();
        }
    }

    public class NetworkPluginModule : Desomnia.Network.PluginModule
    {
        public required NetworkMonitorConfig Config { private get; init; }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<TraefikAuthListener>()
                .As<INetworkService>()
                .SingleInstance();
        }
    }
}
