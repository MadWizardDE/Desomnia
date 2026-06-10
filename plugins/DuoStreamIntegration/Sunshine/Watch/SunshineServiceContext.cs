using Autofac;
using MadWizard.Desomnia.Network;
using MadWizard.Desomnia.Network.Context;
using MadWizard.Desomnia.Network.Watch;

namespace MadWizard.Desomnia.Service.Duo.Sunshine.Watch
{
    internal class SunshineServiceContext : NetworkServiceContext
    {
        public SunshineServiceContext(ILifetimeScope parent, SunshineService service) : base(parent)
        {
            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkServiceLifetimeScopeTag, builder =>
            {
                RegisterService(builder, service);

                builder.RegisterType<ServiceFilterWatch>().As<NetworkServiceWatch>()
                    //.WithProperty(TypedParameter.From(info.MakeAdvertiseOptions())) // TODO MakeAdvertiseOptions ??
                    .OnActivated(args => args.Instance.IsHidden = true)
                    .SingleInstance()
                    .AsSelf();
            });
        }
    }
}
