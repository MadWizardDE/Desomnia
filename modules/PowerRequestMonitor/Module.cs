using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Power.Manager;
using MadWizard.Desomnia.PowerRequest.Configuration;

namespace MadWizard.Desomnia.PowerRequest
{
    public class Module : Desomnia.ConfigurableModule<ModuleConfig>
    {
        protected override void Load(ContainerBuilder builder, ModuleConfig config)
        {
            if (config.PowerRequestMonitor is PowerRequestMonitorConfig monitor)
            {
                builder.RegisterType<PowerRequestMonitor>()
                    .OnlyIf(reg => reg.IsRegistered(new TypedService(typeof(IPowerManager))))
                    .WithParameter(TypedParameter.From(monitor.RequestFilterRule))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();
            }
        }
    }
}
