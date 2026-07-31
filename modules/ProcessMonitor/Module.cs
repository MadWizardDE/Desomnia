using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Processes.Configuration;
using MadWizard.Desomnia.Processes.Manager;

namespace MadWizard.Desomnia.Processes
{
    public class Module : Desomnia.ConfigurableModule<ModuleConfig>
    {
        protected override void Load(ContainerBuilder builder, ModuleConfig config)
        {
            var manager = config.ProcessMonitor as ProcessManagerConfig;

            // fallback, if no better ProcessManager is available
            builder.RegisterType<PollingProcessManager>().As<ProcessManager>().AsImplementedInterfaces().AsSelf()
                .WithParameter(TypedParameter.From(manager?.PollInterval ?? ProcessManagerConfig.DefaultPollInterval))
                .OnlyIf(reg => !reg.IsRegistered(new TypedService(typeof(IProcessManager))))
                .SingleInstance();

            if (config.ProcessMonitor is ProcessMonitorConfig monitor)
            {
                builder.RegisterType<ProcessMonitor>()
                    .WithParameter(new TypedParameter(typeof(ProcessMonitorConfig), monitor))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

                builder.RegisterType<ProcessWatch>().AsSelf();
            }
        }
    }
}
