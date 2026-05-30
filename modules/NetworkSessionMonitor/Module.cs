using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.NetworkSession.Configuration;
using MadWizard.Desomnia.NetworkSession.Manager;
using Microsoft.Extensions.Configuration.Xml;

namespace MadWizard.Desomnia.NetworkSession
{
    public class Module : Desomnia.ConfigurableModule<ModuleConfig>
    {
        protected override void ConfigureConfigurationSource(ExtendedXmlConfigurationSource source)
        {
            source.AddNamelessCollectionElement("FilterRule");
        }

        protected override void Load(ContainerBuilder builder)
        {
            if (Config.NetworkSessionMonitor is not null)
            {
                builder.RegisterType<NetworkSessionMonitor>()
                    .OnlyIf(reg => reg.IsRegistered(new TypedService(typeof(INetworkSessionManager))))
                    .WithParameter(TypedParameter.From(Config.NetworkSessionMonitor.MakeWatchOptions()))
                    .WithParameter(TypedParameter.From(Config.NetworkSessionMonitor.FilterRule))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();
            }
        }
    }
}
