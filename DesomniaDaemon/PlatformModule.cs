using Autofac;
using MadWizard.Desomnia.Daemon.Configuration;
using MadWizard.Desomnia.Daemon.DBus;
using MadWizard.Desomnia.Daemon.DBus.Interface;
using MadWizard.Desomnia.Network;
using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Power.Manager;
using Microsoft.Extensions.Configuration.Xml;

namespace MadWizard.Desomnia.Daemon
{
    internal class PlatformModule : Desomnia.ConfigurableModule<DaemonConfig>
    {
        private static string HostsFilePath => "/etc/hosts";

        // The D-Bus system bus socket is the canonical indicator that logind is reachable.
        private static bool HasSystemDBus() => File.Exists("/run/dbus/system_bus_socket");

        protected override void Load(ContainerBuilder builder)
        {
            // Implementing Power-Manager
            if (Config.UseDBus && HasSystemDBus())
            {
                builder.RegisterType<DBusManager>()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

                // TODO: this is only a quick fix
                builder.Register<ILogin1Manager>(ctx => ctx.Resolve<DBusManager>().LoginManager);

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

            // Options mappings
            builder.RegisterType<HostsManager>()
                .WithParameter(TypedParameter.From(HostsFilePath))
                .AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<LinuxNeighborCache>()
                .AsImplementedInterfaces()
                .InstancePerNetwork();

            if (EthtoolOperator.IsInstalled)
            {
                builder.RegisterType<EthtoolOperator>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork();
            }
        }

        protected override void ConfigureConfigurationSource(ExtendedXmlConfigurationSource source)
        {
            source.AddEnumAttribute("watchOperation")
                  .AddEnumAttribute("watchMode");
        }
    }
}
