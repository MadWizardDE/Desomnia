using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Network;
using MadWizard.Desomnia.Service.Duo.Configuration;
using MadWizard.Desomnia.Service.Duo.Manager;
using MadWizard.Desomnia.Service.Duo.Sunshine.Listener;
using MadWizard.Desomnia.Service.Duo.Sunshine.Watch;
using System.ServiceProcess;
using WindowsFirewallHelper;

namespace MadWizard.Desomnia.Service.Duo
{
    public class PluginModule : Desomnia.ConfigurableModule<DuoConfig>
    {
        protected override void Load(ContainerBuilder builder)
        {
            if (Config.DuoStreamMonitor is DuoStreamMonitorConfig config)
            {
                var monitor = builder.RegisterType<DuoStreamMonitor>()
                    .WithParameter(TypedParameter.From(config))
                    .AsImplementedInterfaces().AsSelf()
                    .SingleInstance();

                monitor.OnActivated(args =>
                {
                    args.Instance.AddEventAction(nameof(DuoStreamMonitor.Idle), config.OnIdle);
                    args.Instance.AddEventAction(nameof(DuoStreamMonitor.Demand), config.OnDemand);
                });

                if (!config.UsePolling)
                {
                    try
                    {
                        using var service = new ServiceController(config.ServiceName);

                        if (service.GetVersion() >= DuoEventManager.MinVersion)
                        {
                            builder.RegisterType<DuoEventManager>().As<DuoManager>()
                                .WithParameter(TypedParameter.From(config))
                                .AsImplementedInterfaces()
                                .SingleInstance();

                            goto skipPolling;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Duo Service is not available
                    }
                }

                builder.RegisterType<DuoPollingManager>().As<DuoManager>()
                    .WithParameter(TypedParameter.From(config))
                    .AsImplementedInterfaces()
                    .SingleInstance();

            skipPolling:

                builder.RegisterModule<SunshineListenerModule>()
                    .OnlyIf(reg => config.UseFallback ||
                        !reg.IsRegistered(new TypedService(typeof(DynamicNetworkObserver))));

                if (!config.UseFallback)
                    builder.RegisterType<NetworkPluginModule>()
                        .As<Desomnia.Network.PluginModule>()
                        .SingleInstance();
            }
        }
    }

    internal class SunshineListenerModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance<IFirewall>(FirewallWAS.Instance).As<IFirewall>();

            var listener = builder.RegisterType<SunshineListener>()
                .InstancePerDependency()
                .AsSelf();

            // trigger WaitForClient(), if Sunshine is not running
            listener.OnActivated(args => args.Instance.Inspect(TimeSpan.Zero));

            builder.RegisterType<SunshineListenerAdapter>()
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }

    public class NetworkPluginModule : Desomnia.Network.PluginModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<SunshineServiceContext>()
                .InstancePerDependency()
                .AsSelf();

            builder.RegisterType<SunshineServiceContextAdapter>()
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }
}
