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
                    .WithParameter(TypedParameter.From(network))
                    .WithMetadata("name", network.Name)
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
