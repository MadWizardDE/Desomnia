using Autofac;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Discovery;
using MadWizard.Desomnia.Network.Discovery.BuiltIn;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Context
{
    public partial class NetworkContext
    {
        private static void RegisterRouterDiscovery(ContainerBuilder builder, NetworkMonitorConfig config)
        {
            // Router/Options-Discovery
            builder.RegisterType<DefaultGatewayDetector>()
                .WithParameter(TypedParameter.From(config.AutoDetect))
                .WithParameter(TypedParameter.From(config.MakeAutoDiscoveryOptions()))
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();
            builder.RegisterType<RouterAdvertismentDetector>()
                .WithParameter(TypedParameter.From(config.AutoDetect))
                .WithParameter(TypedParameter.From(config.MakeAutoDiscoveryOptions()))
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();
        }

        internal async Task DiscoverRouters()
        {
            Logger.LogDebug("Discovering routers...");

            // register static routers
            foreach (var configRouter in Config.Router)
            {
                foreach (var configVPNClient in configRouter.VPNClient)
                {
                    CreateHost(new TypedParameter(typeof(NetworkHostInfo), configVPNClient));
                }

                CreateHost(new TypedParameter(typeof(NetworkRouterInfo), configRouter));
            }

            // register dynamic routers
            if (Config.AutoDetect.HasFlag(AutoDiscoveryType.Router))
            {
                if (Scope.ResolveOptional<IRouterDiscovery>() is IRouterDiscovery discovery)
                {
                    await discovery.DiscoverRouters(Network);
                }
            }
        }
    }
}