using Autofac;
using MadWizard.Desomnia.Daemon.Configuration;
using MadWizard.Desomnia.Network;
using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Power.Manager;
using Microsoft.Extensions.Configuration.Xml;

namespace MadWizard.Desomnia.Daemon
{
    internal class PlatformModule : Desomnia.ConfigurableModule<DaemonConfig>
    {
        // The D-Bus system bus socket is the canonical indicator that logind is reachable.
        private static bool HasSystemDBus() => File.Exists("/run/dbus/system_bus_socket");

        protected override void Load(ContainerBuilder builder)
        {
            if (Config.UseDBus && HasSystemDBus())
            {
                builder.RegisterType<DBusPowerManager>()
                    .WithParameter(TypedParameter.From(Config.PowerRequestMonitor.WatchOperation))
                    .WithParameter(TypedParameter.From(Config.PowerRequestMonitor.WatchMode))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();
            }
            else // fallback
            {
                builder.RegisterType<SysPowerManager>()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();
            }

            // Implementing Network-Managers
            builder.RegisterType<LinuxNeighborCache>()
                .AsImplementedInterfaces()
                .InstancePerNetwork()
                .AsSelf();
        }

        protected override void ConfigureConfigurationSource(ExtendedXmlConfigurationSource source)
        {
            source.AddEnumAttribute("watchOperation")
                  .AddEnumAttribute("watchMode");
        }
    }
}
