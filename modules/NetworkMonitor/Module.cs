using Autofac;
using MadWizard.Desomnia.Network.Address;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Context;
using MadWizard.Desomnia.Network.Demand;
using MadWizard.Desomnia.Network.Demand.Detector;
using MadWizard.Desomnia.Network.Discovery;
using MadWizard.Desomnia.Network.Filter;
using MadWizard.Desomnia.Network.Handoff;
using MadWizard.Desomnia.Network.Knocking;
using MadWizard.Desomnia.Network.Knocking.Methods;
using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Network.Manager.Guard;
using MadWizard.Desomnia.Network.Middleware;
using MadWizard.Desomnia.Network.Reachability;
using MadWizard.Desomnia.Power.Guard;
using Microsoft.Extensions.Configuration.Xml;
using System.ComponentModel;

namespace MadWizard.Desomnia.Network
{
    public class Module : Desomnia.ConfigurableModule<ModuleConfig>
    {
        protected override void ConfigureConfigurationSource(ExtendedXmlConfigurationSource source)
        {
            source  .AddBooleanAttribute("must", new() { ["type"] = "Must" })
                    .AddNamelessCollectionElement("NetworkMonitor", (e, nr) => NetworkMonitorConfig.NAMLESS_PREFIX + nr)
                    .AddNamelessCollectionElement("SharedSecret")
                    .AddNamelessCollectionElement("ServiceFilterRule")
                    .AddNamelessCollectionElement("HostFilterRule")
                    .AddNamelessCollectionElement("HostRangeFilterRule")
                    .AddNamelessCollectionElement("HostRange")
                    .AddNamelessCollectionElement("Host")
                    .AddNamelessCollectionElement("HTTPFilterRuleInfo")
                    .AddNamelessCollectionElement("RequestFilterRule")
                    .AddEnumAttribute("autoDetect")
                    .AddEnumAttribute("advertise")
                    .AddEnumAttribute("handoff")
                    .AddEnumAttribute("protocol")
                    .AddEnumAttribute("sleepProxyDiscovery")
                    .AddEnumAttribute("wakeType");
        }

        protected override void Load(ContainerBuilder builder)
        {
            if (Config.NetworkMonitor.Count > 0)
            {
                builder.RegisterType<DynamicNetworkObserver>()
                    .WithParameter(TypedParameter.From(Config))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

                // Composites //

                builder.RegisterComposite<CompositeWatchedServiceDiscovery, IWatchedServiceDiscovery>();
                builder.RegisterComposite<CompositeServiceDiscovery, IServiceDiscovery>();
                builder.RegisterComposite<CompositeRouterDiscovery, IRouterDiscovery>();
                builder.RegisterComposite<CompositeIPAddressDiscovery, IIPAddressDiscovery>();
                builder.RegisterComposite<CompositePhysicalAddressDiscovery, IPhysicalAddressDiscovery>();
                builder.RegisterComposite<CompositeVirtualMachineManager, IVirtualMachineManager>();

                // Knock-Methods
                builder.RegisterType<PlainTextKnockMethod>()
                    .Named<IKnockMethod>("plain")
                    .Named<IKnockDetector>("plain")
                    .AsImplementedInterfaces()
                    .SingleInstance();

                // Network Context //

                builder.RegisterType<NetworkContext>()
                    .InstancePerOwned<NetworkContext>()
                    .AsSelf();

                // Global Network Filters //

                builder.RegisterType<TrafficFilterRequest>()
                    .InstancePerDependency()
                    .AsSelf();
                builder.RegisterType<LocalPacketFilter>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork();

                // Packet Filters //

                builder.RegisterType<RouterFilter>()
                    .As<IPacketFilter>()
                    .InstancePerNetwork();
                builder.RegisterType<PacketRuleFilter>()
                    .As<IPacketFilter>()
                    .InstancePerMatchingLifetimeScope(
                        MatchingScopeLifetimeTags.NetworkHostLifetimeScopeTag,
                        MatchingScopeLifetimeTags.NetworkServiceLifetimeScopeTag);

                builder.RegisterComposite<CompositePacketFilter, IPacketFilter>();

                // Network Services //

                // Safeguard around the platform neighbour cache: track installed mappings, suppress
                // deletes of entries already removed, and purge any leftovers when the network
                // scope is disposed.
                builder.RegisterDecorator<GuardedLocalAddressMapping, ILocalAddressMapping>();

                builder.RegisterType<AddressMappingService>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork()
                    .AsSelf();
                builder.RegisterType<ReachabilityService>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork()
                    .AsSelf();
                builder.RegisterType<ReachabilityCache>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork()
                    .AsSelf();
                builder.RegisterType<DemandService>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork()
                    .AsSelf();
                builder.RegisterType<HandoffService>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork()
                    .AsSelf();
                builder.RegisterType<KnockService>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork()
                    .AsSelf();


                // Demand Triggers //

                builder.RegisterType<DemandByIP>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork();
                builder.RegisterType<DemandByARP>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork();
                builder.RegisterType<DemandByNDP>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork();
                builder.RegisterType<DemandByWOL>()
                    .AsImplementedInterfaces()
                    .InstancePerNetwork();

                // --- Request Scope ---- //

                builder.RegisterType<DemandRequest>()
                    .InstancePerRequest()
                    .AsSelf();

                // Make NetworkMonitors dynamically available
                builder.RegisterServiceMiddleware<IEnumerable<IInspectable>>(new DynamicNetworkMonitors<IInspectable>());
                builder.RegisterServiceMiddleware<IEnumerable<IPowerTransitionGuard>>(new DynamicNetworkMonitors<IPowerTransitionGuard>());
                builder.RegisterServiceMiddleware<IEnumerable<NetworkMonitor>>(new DynamicNetworkMonitors<NetworkMonitor>());
            }
        }
    }

    public abstract class PluginModule : Autofac.Module
    {
        //public virtual void Build(ContainerBuilder builder) { }

        public class Metadata
        {
            [DefaultValue(null)]
            public string? Name { get; set; }
        }
    }

}
