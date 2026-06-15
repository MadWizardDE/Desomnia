using Autofac;
using MadWizard.Desomnia.Network.Configuration.Services;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using MadWizard.Desomnia.Network.Neighborhood.Services;
using MadWizard.Desomnia.Network.Watch;

namespace MadWizard.Desomnia.Network.Context
{
    public class NetworkServiceContext : FilterContext
    {
        public NetworkService       Service     => field ??= Scope.Resolve<NetworkService>();
        public NetworkServiceWatch  Watch       => field ??= Scope.Resolve<NetworkServiceWatch>();

        public ServiceOptions Options { get; init; }

        protected NetworkServiceContext(ILifetimeScope parent, ServiceOptions options = default) : base(parent) { Options = options; }

        public NetworkServiceContext(ILifetimeScope parent, WatchedServiceInfo info, ServiceOptions options = default) : this(parent, options)
        {
            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkServiceLifetimeScopeTag, builder =>
            {
                var service = builder.RegisterType<TransportNetworkService>().As<NetworkService>()
                    .WithParameter(TypedParameter.From(info.Name))
                    .WithParameter(TypedParameter.From(info.IPPort))
                    .SingleInstance()
                    .AsSelf();

                if (info.ServiceName is string name)
                {
                    service.WithProperty(TypedParameter.From(info.ServiceName));
                }

                var watch = builder.RegisterType<ServiceFilterWatch>().As<NetworkServiceWatch>()
                    .WithProperty(TypedParameter.From(info.MakeAdvertiseOptions()))
                    .WithProperty(TypedParameter.From(info.MakeKnockOptions()))
                    .WithProperty(TypedParameter.From(info.MinTraffic))
                    .SingleInstance()
                    .AsSelf();

                watch.OnActivated(args =>
                {
                    args.Instance.ShouldHandoffToSleepProxy = info.Handoff;

                    args.Instance.AddEventAction(nameof(NetworkServiceWatch.Demand), info.OnDemand);
                    args.Instance.AddEventAction(nameof(NetworkServiceWatch.Idle), info.OnIdle);
                });

                RegisterServiceFilter(builder, info);
            });
        }

        protected void RegisterService(ContainerBuilder builder, TransportNetworkService service)
        {
            builder.RegisterInstance(service).As<NetworkService>();

            RegisterServiceFilter(builder, service);
        }
    }
}
