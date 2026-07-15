using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Network.SleepProxy.Registration;
using MadWizard.Desomnia.NetworkSession.Configuration;
using MadWizard.Desomnia.NetworkSession.Manager;

namespace MadWizard.Desomnia.NetworkSession
{
    public class Module : Desomnia.ConfigurableModule<ModuleConfig>
    {
        protected override void Load(ContainerBuilder builder)
        {
            if (Config.NetworkSessionMonitor is NetworkSessionMonitorConfig config)
            {
                builder.RegisterType<NetworkSessionMonitor>()
                    .OnlyIf(reg => reg.IsRegistered(new TypedService(typeof(INetworkSessionManager))))
                    .WithParameter(TypedParameter.From(config.MakeWatchOptions()))
                    .WithParameter(TypedParameter.From(config.FilterRule))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

                if (config.RegisterWithSleepProxy)
                {
                    // Add SMB port to SleepProxyRegistration
                    builder.ComponentRegistryBuilder.Registered += (sender, args) =>
                    {
                        if (args.ComponentRegistration.IsLimitedTo<SleepProxyRegistration>())
                            args.ComponentRegistration.PipelineBuilding += (_, pipeline) =>
                                pipeline.Use(new SMBSleepProxyRegistration());
                    };
                }
            }
        }
    }
}
