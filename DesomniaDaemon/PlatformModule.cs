using Autofac;
using MadWizard.Desomnia.Network;
using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Power.Manager;

namespace MadWizard.Desomnia.Daemon
{
    internal class PlatformModule : Desomnia.Module
    {
        // The D-Bus system bus socket is the canonical indicator that logind is reachable.
        private static bool IsLogindAvailable() =>
            File.Exists("/run/dbus/system_bus_socket");

        protected override void Load(ContainerBuilder builder)
        {
            // Implementing Platform-Managers
            if (IsLogindAvailable())
            {
                builder.RegisterType<LogindPowerManager>()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();
            }
            else
            {
                builder.RegisterType<SysfsPowerManager>()
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
    }
}
