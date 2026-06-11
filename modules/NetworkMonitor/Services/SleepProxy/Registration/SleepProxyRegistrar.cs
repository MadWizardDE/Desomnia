using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Context;
using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    internal class SleepProxyRegistrar(AutoDiscoveryType auto, SleepProxyOptions options)
    {
        public required ILogger<SleepProxyRegistrar> Logger { private get; init; }

        public required NetworkContext Context { private get; set; }

        readonly ConcurrentDictionary<PhysicalAddress, SleepProxyLease> _activeLeases = [];

        public SleepProxyLease Register(SleepProxyRegistration reg)
        {
            if (!_activeLeases.TryGetValue(reg.PrimaryAddress, out var lease))
            {
                lease = new SleepProxyLease
                {
                    Sequence = reg.Sequence,
                    Duration = options.DetermineLeaseDuration(reg.RequestedLease)
                };
            }
            else if (lease.Sequence >= reg.Sequence)
            {
                return lease;
            }
            else
            {
                // TODO update lease time?
            }

            if (Context.FindHostContextBy(reg.PrimaryAddress) is not NetworkHostContext ctxHost)
            {
                ctxHost = CreateHost(reg);

                lease.AddInstanceForDisposal(ctxHost);
            }

            if (ctxHost.Watch is not RemoteHostWatch remote)
                throw new NotSupportedException("Service registration is only supported for watched remote hosts.");

            if (ctxHost.Auto.HasFlag(AutoDiscoveryType.Service))
            {
                foreach (var serviceInfo in reg.Services)
                {
                    if (ctxHost.FindServiceContextBy(serviceInfo.IPPort) is NetworkServiceContext ctxService)
                        throw new NotSupportedException($"Already watching service at {serviceInfo.IPPort} for {ctxHost.Host.Name}.");

                    ctxService = ctxHost.CreateWatchedService(serviceInfo);

                    lease.AddInstanceForDisposal(ctxService);
                }
            }
            else if (reg.Services.Count > 0)
            {
                throw new NotSupportedException($"Registration of services is not configured for {ctxHost.Host.Name}.");
            }

            foreach (var adr in reg.IPAddresses.Where(adr => adr.Key.AddressFamily.ShouldDiscover(ctxHost.Auto)))
            {
                if (ctxHost.Host.AddAddress(adr.Key, adr.Value))
                {
                    Logger.LogHostAddressAdded(ctxHost.Host, adr.Key);
                }
            }

            _activeLeases[reg.PrimaryAddress] = lease;

            return lease;
        }

        private NetworkHostContext CreateHost(SleepProxyRegistration reg)
        {
            List<Parameter> parameters = [];

            RemoteHostInfo hostInfo;
            if (reg.TargetAddress is PhysicalAddress target) // is this a virtual machine?
            {
                hostInfo = new RemoteVirtualHostInfo() { Name = reg.Name };

                var ctxPhysical = Context.FindHostContextBy(target) ?? throw new KeyNotFoundException($"Wake host with MAC = {target} not found.");

                if (!ctxPhysical.Auto.HasFlag(AutoDiscoveryType.Host))
                    throw new NotSupportedException($"Host {ctxPhysical.Host.Name} is not configured to discover virtual hosts.");

                parameters.Add(TypedParameter.From(new RemotePhysicalHostInfo() { Name = ctxPhysical.Host.Name }));
            }
            else
            {
                if (!auto.HasFlag(AutoDiscoveryType.Host))
                    throw new NotSupportedException("Registration of unknown hosts is not configured.");

                hostInfo = new RemotePhysicalHostInfo() { Name = reg.Name };
            }

            hostInfo.AutoDetect = AutoDiscoveryType.IP | AutoDiscoveryType.Host | AutoDiscoveryType.Service;

            hostInfo.HostName = reg.Hostname;
            hostInfo.MAC = reg.PrimaryAddress;

            hostInfo.WakePasswordBytes = reg.Password;

            return Context.CreateDynamicHost([ new TypedParameter(hostInfo.GetType(), hostInfo), .. parameters ]);
        }
    }
}
