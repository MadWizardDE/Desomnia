using Autofac;
using Autofac.Features.Metadata;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Context.Parameters;
using MadWizard.Desomnia.Network.Context.Watch;
using MadWizard.Desomnia.Network.Filter;
using MadWizard.Desomnia.Network.Filter.Rules;
using MadWizard.Desomnia.Network.Knocking;
using MadWizard.Desomnia.Network.Logging;
using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Network.Middleware;
using MadWizard.Desomnia.Network.Naming.MDNS;
using MadWizard.Desomnia.Network.Naming.MDNS.Resolver;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.SleepProxy;
using MadWizard.Desomnia.Network.Trace;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Context
{
    public partial class NetworkContext : FilterContext, IIEnumerable<NetworkHostContext>
    {
        public ILogger<NetworkContext> Logger { private get; init; }

        public required IVirtualMachineManager VMManager { private get; init; }

        public string Name { get; private init; }

        public NetworkMonitorConfig Config { get; init; }

        public IEnumerable<PluginModule> Plugins { get; private init; }

        public NetworkDevice    Device      { get => field ??= Scope.Resolve<NetworkDevice>();  }
        public NetworkSegment   Network     { get => field ??= Scope.Resolve<NetworkSegment>(); }
        public NetworkMonitor   Monitor     { get => field ??= Scope.Resolve<NetworkMonitor>(); }

        public NetworkInterface Interface
        {
            get => Device.Interface;

            set
            {
                Device.Interface = value;

                if (IsSuspended)
                {
                    IsSuspended = false;

                    Monitor.ResumeMonitoring();
                }
            }
        }

        internal bool IsSuspended
        {
            get => field;

            set
            {
                if (field != value)
                {
                    if (field = value)
                    {
                        Monitor.SuspendMonitoring();
                    }
                }
            }
        }

        private readonly IList<NetworkHostContext> _hostContexts = [];
        private readonly IList<NetworkKnockContext> _knockContexts = [];

        public NetworkContext(ILifetimeScope parent, NetworkMonitorConfig config, NetworkInterface @interface) : base(parent)
        {
            Config = config;

            Name = Config.Name ?? @interface.Name;

            Plugins = parent.Resolve<IEnumerable<Meta<PluginModule>>>()
                .Where(x => x.Metadata["name"] is not string name || name == config.Name)
                .Select(x => x.Value);

            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkLifetimeScopeTag, builder =>
            {
                builder.RegisterInstance(this); // dirty little hack, to make the Network available during the construction of the child scope

                RegisterContextAwareLogger(parent, builder);

                builder.RegisterType<NetworkMonitor>()
                    .WithParameter(TypedParameter.From(Name))
                    .WithParameter(TypedParameter.From(config.MakeWatchOptions()))
                    .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies)
                    .OnActivated(args =>
                    {
                        args.Instance.AddEventAction(nameof(NetworkMonitor.Idle), config.OnIdle);
                        args.Instance.AddEventAction(nameof(NetworkMonitor.Demand), config.OnDemand);
                        args.Instance.AddEventAction(nameof(NetworkMonitor.Connected), config.OnConnect);
                        args.Instance.AddEventAction(nameof(NetworkMonitor.Disconnected), config.OnDisconnect);
                    })
                    .SingleInstance()
                    .AsSelf();

                builder.RegisterType<NetworkDevice>()
                    .OnPreparing(e => e.Parameters = [TypedParameter.From(@interface)])
                    .ConfigurePipeline(p => p.Use(new DefaultDeviceSelector()))
                    .SingleInstance()
                    .AsSelf();

                builder.RegisterType<NetworkSegment>()
                    .SingleInstance()
                    .AsSelf();


                // Child Contexts
                builder.RegisterType<NetworkHostContext>()
                    .WithParameter(TypedParameter.From(config))
                    .ConfigurePipeline(p => p.Use(new DefaultNetworkServiceOptions(config)))
                    .InstancePerDependency()
                    .AsSelf();
                builder.RegisterType<NetworkHostServiceContext>()
                    .InstancePerDependency()
                    .AsSelf();
                builder.RegisterType<NetworkKnockContext>()
                    .WithParameter(TypedParameter.From(config))
                    .InstancePerDependency()
                    .AsSelf();

                builder.RegisterType<NetworkJanitor>()
                    .WithParameter(TypedParameter.From(config.MakeSweepOptions()))
                    .SingleInstance()
                    .AsSelf();

                if (config.UseBPF)
                {
                    builder.RegisterType<BerkeleyPacketFilter>()
                        .AsImplementedInterfaces()
                        .InstancePerNetwork()
                        .AsSelf();
                }

                if (config.WatchTimeout is TimeSpan timeout)
                {
                    var reg = builder.RegisterType<CaptureWatchDog>().AutoActivate()
                        .WithParameter(TypedParameter.From(timeout))
                        .AsImplementedInterfaces()
                        .InstancePerNetwork()
                        .AsSelf();

                    reg.OnActivated(x => x.Instance.GatewayTimeout = config.PingTimeout);
                }

                if (config.Hosts.Any(h => h.Trace))
                {
                    string[] hosts = [.. config.Hosts.Where(h => h.Trace).Select(h => h.Name)];

                    builder.RegisterType<TraceService>()
                        .WithParameter(TypedParameter.From(new TraceService.Options() { Hosts = hosts }))
                        .AsImplementedInterfaces()
                        .InstancePerNetwork();
                }

                if (config.WatchMode == WatchMode.Promiscuous)
                {
                    builder.RegisterType<PromiscuousModeMutex>()
                        .WithParameter(TypedParameter.From(config.PingTimeout))
                        .AsImplementedInterfaces()
                        .InstancePerNetwork()
                        .AsSelf();

                    var mdns = builder.RegisterType<MulticastDNSService>()
                        .AsImplementedInterfaces()
                        .SingleInstance()
                        .AsSelf();

                    mdns.OnActivated(args =>
                    {
                         args.Instance.ForceUnicast = config.AdvertiseUnicast;
                    });

                    builder.RegisterType<HostnameResolver>()
                        .AsImplementedInterfaces()
                        .SingleInstance();

                    if (true) // TODO when enable Sleep Proxy?
                    {
                        var service = new SleepProxyService(MulticastDNSService.MulticastPort)
                        {
                            /// <summary>
                            /// Desomnia's default metric: deliberately HIGH (poor) so genuine proxies always win — we only
                            /// want to be the last-resort responder, in keeping with the non-invasive design.
                            /// </summary>

                            Metrics = new()
                            {
                                Type            = 90,   // incidental software on a general-purpose host
                                Portability     = 40,   // TODO: detect battery vs. mains and adjust
                                MarginalPower   = 70,
                                TotalPower      = 70,
                            }
                        };

                        builder.RegisterType<SleepProxyResolver>()
                            .WithParameter(new LocalHostParameter<NetworkHost>())
                            .WithParameter(TypedParameter.From(service))
                            .AsImplementedInterfaces()
                            .SingleInstance();

                        builder.RegisterType<SleepProxyServiceResolver>()
                            .AsImplementedInterfaces()
                            .SingleInstance();
                    }

                    RegisterTrafficFilter(builder, new UDPTrafficType(MulticastDNSService.MulticastPort));
                }

                if (config.AllowWakeOnLAN is WakeOnLANMode allow)
                {
                    var build = builder.RegisterType<WakeOnLANConfigurator>()
                        .WithParameter(TypedParameter.From(allow & ~WakeOnLANMode.Default))
                        .AsImplementedInterfaces()
                        .InstancePerNetwork();

                    build.OnActivated(x => x.Instance.ShouldReplace = !allow.HasFlag(WakeOnLANMode.Default));
                }

                RegisterRouterDiscovery(builder, config);
                RegisterAddressDiscovery(builder, config);

                RegisterFilters(builder, config);
                RegisterTrafficFilters(builder, config);
                RegisterHostRanges(builder, config);

                foreach (var plugin in Plugins) builder.RegisterModule(plugin);
            });

            Logger = Scope.Resolve<ILogger<NetworkContext>>();

            Scope.Resolve<KnockService>(TypedParameter.From(_knockContexts.SelectMany(ctx => ctx.Stanzas)));
        }

        private void RegisterContextAwareLogger(ILifetimeScope parent, ContainerBuilder builder)
        {
            builder.RegisterGeneric(typeof(NetworkLogger<>))
               .InstancePerDependency()
               .AsSelf();

            builder.RegisterGeneric((context, typeArguments, parameters) =>
            {
                var t = typeArguments[0];

                var loggerServiceType = typeof(ILogger<>).MakeGenericType(t);
                var wrapperType = typeof(NetworkLogger<>).MakeGenericType(t);

                var rootLogger = parent.Resolve(loggerServiceType); // The actual ILogger implementation is root scoped and should resolve to that.

                return context.Resolve(wrapperType, new TypedParameter(loggerServiceType, rootLogger));
            }).As(typeof(ILogger<>)).InstancePerLifetimeScope();

            //builder.RegisterGenericDecorator(typeof(LoggerContextDecorator<>), typeof(ILogger<>));
        }

        private void RegisterFilters(ContainerBuilder builder, NetworkMonitorConfig config)
        {
            builder.RegisterType<StaticHostFilterRule>().AsSelf();
            builder.RegisterType<DynamicHostFilterRule>().AsSelf();
            builder.RegisterType<StaticHostRangeFilterRule>().AsSelf();
            builder.RegisterType<DynamicHostRangeFilterRule>().AsSelf();

            RegisterHostFilters(builder, config.HostFilterRule);
            RegisterHostRangeFilters(builder, config.HostRangeFilterRule);
            RegisterForeignHostFilter(builder, config.ForeignHostFilterRule);
            RegisterServiceFilters(builder, config.ServiceFilterRules);
            RegisterPingFilter(builder, config.PingFilterRule);
        }

        private void RegisterTrafficFilters(ContainerBuilder builder, NetworkMonitorConfig config)
        {
            HashSet<ITrafficType> shapes =
            [
                new ARPTrafficType(),
                new NDPTrafficType(),
                new WOLTrafficType()
            ];

            if (config.WatchUDPPort is ushort port)
            {
                shapes.Add(new UDPTrafficType(port));
            }

            RegisterTrafficFilter(builder, [.. shapes]);
        }

        private void RegisterHostRanges(ContainerBuilder builder, NetworkMonitorConfig config)
        {
            builder.RegisterType<LocalNetworkRange>()
                .SingleInstance()
                .AsSelf();

            foreach (var range in config.Ranges)
            {
                builder.RegisterType<NetworkHostRange>()
                    .Named<NetworkHostRange>(range.Name)
                    .SingleInstance()
                    .AsSelf();
            }
        }

        public bool Matches(object token)
        {
            if (Device == token)
                return true;
            if (Interface == token)
                return true;
            if (Monitor == token)
                return true;

            return false;
        }

        IEnumerator<NetworkHostContext> IEnumerable<NetworkHostContext>.GetEnumerator() => _hostContexts.GetEnumerator();
    }
}
