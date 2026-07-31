using Autofac;
using MadWizard.Desomnia.Daemon.Configuration;
using MadWizard.Desomnia.Daemon.DBus;
using MadWizard.Desomnia.Daemon.DBus.Interface;
using MadWizard.Desomnia.Daemon.DBus.Interface.Adapter;
using MadWizard.Desomnia.Environments;
using MadWizard.Desomnia.Network;
using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Power.Manager;
using MadWizard.Desomnia.Power.Source;
using MadWizard.Desomnia.Processes.Configuration;
using MadWizard.Desomnia.Processes.Manager;

namespace MadWizard.Desomnia.Daemon
{
    internal class PlatformModule : Desomnia.ConfigurableModule<DaemonConfig>
    {
        private static string HostsFilePath => "/etc/hosts";

        // The D-Bus system bus socket is the canonical indicator that logind is reachable.
        private static bool HasSystemDBus() => File.Exists("/run/dbus/system_bus_socket");

        // ignore unknown arguments: other modules parse their own options from the same argv
        //private static CommandLineOptions ParseOptions(string[] args)
        //{
        //    CommandLineOptions options = new();

        //    new Parser(config => config.IgnoreUnknownArguments = true)
        //        .ParseArguments<CommandLineOptions>(args)
        //        .WithParsed(parsed => options = parsed);

        //    return options;
        //}

        protected override void LoadOnce(ContainerBuilder builder, string[] args)
        {
            // the D-Bus-vs-sysfs choice is process-bound: a reload cannot swap the power-manager
            // implementation under an open logind connection. The global --debug and auto-reload
            // options are parsed by the ApplicationBuilder itself.
            //_useDBus = !ParseOptions(args).NoDBus;

            // the network interface manager lives in the persistent container, so it survives a
            // configuration rebuild with its disable intents and took-down bookkeeping intact;
            // created only on first demand, and recorded so a config-less rebuild can re-attach
            builder.RegisterType<IPNetworkInterfaceManager>()
                .As<INetworkInterfaceManager>()
                .SingleInstance()
                .AsSelf();

            // machine-lifetime power probe, backing the "power" condition of every rebuild
            builder.RegisterType<SysfsPowerSourceProbe>()
                .As<IPowerSource>()
                .SingleInstance();

            builder.RegisterType<PowerSourceCondition>()
                .Named<IEnvironmentCondition>("power");
        }

        protected override void Load(ContainerBuilder builder, DaemonConfig config)
        {
            // Takes the place of the module's own polling fallback (its registration steps aside
            // for any IProcessManager already registered, and platform modules load first): same
            // polling, but a poll that finds nothing new is a single directory read here.
            builder.RegisterType<ProcFSProcessManager>()
                .WithParameter(TypedParameter.From(config.ProcessMonitor?.PollInterval ?? ProcessManagerConfig.DefaultPollInterval))
                .AsImplementedInterfaces()
                .As<ProcessManager>()
                .SingleInstance()
                .AsSelf();

            // Implementing Power-Manager
            if (config.UseDBus && HasSystemDBus())
            {
                builder.RegisterType<DBusManager>()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

                builder.RegisterDBusService<ILogin1Manager, Login1Manager>();

                builder.RegisterType<DBusPowerManager>()
                    .WithParameter(TypedParameter.From(config.PowerRequestMonitor.WatchOperation))
                    .WithParameter(TypedParameter.From(config.PowerRequestMonitor.WatchMode))
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
            builder.RegisterType<IPNeighborCache>()
                .AsImplementedInterfaces()
                .InstancePerNetwork();

            if (EthtoolOperator.IsInstalled)
            {
                builder.RegisterType<EthtoolOperator>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork();
            }
        }

    }
}
