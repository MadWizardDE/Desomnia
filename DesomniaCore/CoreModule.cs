using Autofac;
using MadWizard.Desomnia.Configuration;
using MadWizard.Desomnia.Events;
using MadWizard.Desomnia.Logging;
using MadWizard.Desomnia.Power.Guard;
using MadWizard.Desomnia.Power.Manager;
using MadWizard.Desomnia.Power.Watch;
using NLog;
using NLog.Config;

namespace MadWizard.Desomnia
{
    public class CoreModule : Desomnia.ConfigurableModule<SystemMonitorConfig>
    {
        protected internal override void ConfigureLogging(ISetupExtensionsBuilder builder)
        {
            builder.RegisterLayoutRenderer<SleepTimeLayoutRenderer>("sleep-duration");
        }

        protected internal override void LoadOnce(ContainerBuilder builder)
        {
            // the default failure handler: the console/daemon hosts react to the process exit code.
            // A platform module (which loads first) may register its own — the Windows service does,
            // to set the SCM-visible exit code — and PreserveExistingDefaults keeps that one.
            builder.RegisterType<LoggingFailureHandler>()
                .As<IApplicationFailureHandler>()
                .SingleInstance()
                .PreserveExistingDefaults();
        }

        protected override void Load(ContainerBuilder builder, SystemMonitorConfig config)
        {
            if ((config.Version) < SystemMonitorConfig.MIN_VERSION || (config.Version) > SystemMonitorConfig.MAX_VERSION)
                throw new NotSupportedException($"Unsupported configuration version = {config.Version}");

            builder.RegisterServiceMiddlewareSource(new EventSystemMiddlewareSource());

            builder.RegisterType<ActionManager>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();

            builder.RegisterDecorator<GuardedPowerManager, IPowerManager>();

            builder.RegisterType<SleepWatch>()
                .AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<StartupWatch>()
                .AsImplementedInterfaces()
                .SingleInstance();
            builder.RegisterType<ShutdownWatch>()
                .AsImplementedInterfaces()
                .SingleInstance();


            builder.RegisterType<SystemMonitor>().As<IStartable>()
                .WithParameter(TypedParameter.From(config))
                .SingleInstance()
                .AsSelf();

            if (config.Timeout is TimeSpan interval)
            {
                builder.RegisterType<SystemUsageInspector>()
                    .WithParameter(TypedParameter.From(interval))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();
            }

            builder.RegisterType<AsyncExceptionLogger>()
                .AsImplementedInterfaces()
                .SingleInstance();

        }
    }
}
