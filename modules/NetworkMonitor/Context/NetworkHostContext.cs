using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Context.Parameters;
using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;
using NLog;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Context
{
    public partial class NetworkHostContext : FilterContext, IIEnumerable<NetworkHostServiceContext>
    {
        public AutoDiscoveryType    Auto    { get; private set; }

        public NetworkHost          Host    { get => field ??= Scope.Resolve<NetworkHost>();                private init; }
        public NetworkHostWatch?    Watch   { get => field ??= Scope.ResolveOptional<NetworkHostWatch>();   private init; }

        private readonly IList<NetworkHostServiceContext> _serviceContexts = [];

        // Host
        public NetworkHostContext(ILifetimeScope parent, NetworkMonitorConfig configNetwork, NetworkHostInfo config) : base(parent)
        {
            Auto = config.AutoDetect ?? configNetwork.AutoDetect;

            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                var reg = builder.RegisterType<NetworkHost>().As<NetworkHost>()
                    .OnActivated(args => ConfigureHost(args, config))
                    .WithParameter(new TypedParameter(typeof(string), config.Name))
                    .SingleInstance()
                    .AsSelf();
            });
        }

        // Router
        public NetworkHostContext(ILifetimeScope parent, NetworkMonitorConfig configNetwork, NetworkRouterInfo config) : base(parent)
        {
            Auto = config.AutoDetect ?? configNetwork.AutoDetect;

            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                builder.RegisterType<NetworkRouter>().As<NetworkHost>()
                    .WithParameter(new TypedParameter(typeof(string), config.Name))
                    .WithParameter(NetworkHostsParameter.FindBy([.. config.VPNClient.Select(h => h.Name)]))
                    .WithParameter(TypedParameter.From(config.Options))
                    .OnActivated(args => ConfigureHost(args, config))
                    .SingleInstance()
                    .AsSelf();
            });
        }

        // LocalHost
        public NetworkHostContext(ILifetimeScope parent, NetworkMonitorConfig configNetwork, LocalHostInfo config) : base(parent)
        {
            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                builder.RegisterType<LocalHost>().As<NetworkHost>()
                    .OnActivated(args => ConfigureLocalHost(args, config))
                    .SingleInstance()
                    .AsSelf();

                RegisterHostFilters(builder, config.HostFilterRule);
                RegisterHostRangeFilters(builder, config.HostRangeFilterRule);

                builder.RegisterType<LocalHostWatch>().As<NetworkHostWatch>()
                    .WithParameter(TypedParameter.From(config.MakeAdvertiseOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeHandoffOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeDemandOptions(configNetwork)))
                    .WithProperty(TypedParameter.From(config.MinTraffic))
                    .SingleInstance()
                    .AsSelf();
            });

            CreateStaticServices(config.Services);
        }

        // LocalVirtualHost
        public NetworkHostContext(ILifetimeScope parent, NetworkMonitorConfig configNetwork, LocalVirtualHostInfo config, IVirtualMachine vm) : base(parent)
        {
            Auto = config.AutoDetect ?? configNetwork.AutoDetect;

            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                builder.RegisterType<VirtualNetworkHost>().As<NetworkHost>()
                    .WithParameter(new TypedParameter(typeof(string), config.Name))
                    .WithParameter(new LocalHostParameter<NetworkHost>())
                    .WithProperty(TypedParameter.From(vm.Address))
                    .OnActivated(args => ConfigureHost(args, config))
                    .SingleInstance()
                    .AsSelf();

                RegisterFilters(builder, config);

                builder.RegisterType<LocalVirtualHostWatch>().As<NetworkHostWatch>()
                    .WithParameter(TypedParameter.From(vm))
                    .WithParameter(TypedParameter.From(config.MakeAdvertiseOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeHandoffOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeDemandOptions(configNetwork)))
                    .OnActivated(args => ConfigureWatch(args, config))
                    .SingleInstance()
                    .AsSelf();
            });

            CreateStaticServices(config.Services);
        }

        // RemoteHost
        public NetworkHostContext(ILifetimeScope parent, NetworkMonitorConfig configNetwork, RemotePhysicalHostInfo config) : base(parent)
        {
            Auto = config.AutoDetect ?? configNetwork.AutoDetect;

            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                var reg = builder.RegisterType<NetworkHost>().As<NetworkHost>()
                    .WithParameter(new TypedParameter(typeof(string), config.Name))
                    .OnActivated(args => ConfigureHost(args, config))
                    .SingleInstance()
                    .AsSelf();

                RegisterFilters(builder, config);

                builder.RegisterType<RemoteHostWatch>().As<NetworkHostWatch>()
                    .WithParameter(TypedParameter.From(config.MakeAdvertiseOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeHandoffOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeDemandOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakePingOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeWakeOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeHandoffOptions(configNetwork)))
                    .OnActivated(args => ConfigureWatch(args, config))
                    .SingleInstance()
                    .AsSelf();
            });

            CreateStaticServices(config.Services);
        }

        // RemoteVirtualHost
        public NetworkHostContext(ILifetimeScope parent, NetworkMonitorConfig configNetwork, RemoteVirtualHostInfo config, RemotePhysicalHostInfo configPhysical) : base(parent)
        {
            Auto = config.AutoDetect ?? configNetwork.AutoDetect;

            Scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag, builder =>
            {
                builder.RegisterType<VirtualNetworkHost>().As<NetworkHost>()
                    .WithParameter(new TypedParameter(typeof(string), config.Name))
                    .WithParameter(new NetworkHostParameter<NetworkHost>(configPhysical.Name))
                    .OnActivated(args => ConfigureHost(args, config))
                    .SingleInstance()
                    .AsSelf();

                RegisterFilters(builder, config);

                builder.RegisterType<RemoteVirtualHostWatch>().As<NetworkHostWatch>()
                    .WithParameter(NetworkHostWatchParameter<RemoteHostWatch>.FindByHostName(configPhysical.Name))
                    .WithParameter(TypedParameter.From(config.MakeAdvertiseOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeHandoffOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeDemandOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakePingOptions(configNetwork)))
                    .WithParameter(TypedParameter.From(config.MakeWakeOptions(configNetwork)))
                    .OnActivated(args => ConfigureWatch(args, config))
                    .SingleInstance()
                    .AsSelf();
            });

            CreateStaticServices(config.Services);
        }

        private static void ConfigureHost(IActivatedEventArgs<NetworkHost> args, NetworkHostInfo config)
        {
            var host = args.Instance;

            var logger = args.Context.Resolve<ILogger<NetworkHostContext>>();

            using (logger.BeginHostScope(host))
            {
                logger.LogDebug("Configuring host '{name}':", config.Name);

                // Configure hostname
                if (config.HostName != null)
                {
                    host.HostName = config.HostName;
                }

                // Configure static MAC address
                if ((host.PhysicalAddress ??= config.MAC) is PhysicalAddress mac)
                {
                    logger.LogHostPhysicalAddressChanged(host, mac);
                }

                // Configure static IP addresses
                foreach (var ip in config.IPAddresses)
                {
                    if (host.AddAddress(ip, new(IPAddressFlags.Static)))
                    {
                        logger.LogHostAddressAdded(host, ip);
                    }
                }
            }
        }

        private static void ConfigureLocalHost(IActivatedEventArgs<LocalHost> args, LocalHostInfo config)
        {
            var logger = args.Context.Resolve<ILogger<NetworkHostContext>>();

            if (logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            {
                var host = args.Instance;

                using (logger.BeginHostScope(host))
                {
                    logger.LogDebug("Configuring localhost:");

                    if (host.PhysicalAddress is PhysicalAddress mac)
                        logger.LogHostPhysicalAddressChanged(host, mac);

                    foreach (var ip in host.IPAddresses)
                        logger.LogHostAddressAdded(host, ip);
                }
            }
        }

        private static void ConfigureWatch(IActivatedEventArgs<NetworkHostWatch> args, WatchedHostInfo config)
        {
            var watch = args.Instance;

            watch.Threshold = config.MinTraffic;

            if (watch is HostDemandWatch)
            {
                watch.AddEventAction(nameof(HostDemandWatch.Demand), config.OnDemand);
                watch.AddEventAction(nameof(HostDemandWatch.Idle), config.OnIdle);

                watch.AddEventAction(nameof(HostDemandWatch.Started), config.OnStart);
                watch.AddEventAction(nameof(HostDemandWatch.Suspended), config.OnSuspend);
                watch.AddEventAction(nameof(HostDemandWatch.Stopped), config.OnStop);

                watch.AddEventAction(nameof(HostDemandWatch.MagicPacket), config.OnMagicPacket);
            }
        }

        IEnumerator<NetworkHostServiceContext> IEnumerable<NetworkHostServiceContext>.GetEnumerator() => _serviceContexts.GetEnumerator();
    }
}
