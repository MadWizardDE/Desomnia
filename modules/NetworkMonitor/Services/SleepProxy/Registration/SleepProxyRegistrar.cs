using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Context;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    internal class SleepProxyRegistrar(SleepProxyOptions options)
    {
        public required ILogger<SleepProxyRegistrar> Logger { private get; init; }

        public required NetworkContext Context { private get; set; }

        readonly HashSet<SleepProxyLease> _activeLeases = [];

        public SleepProxyLease Register(SleepProxyRegistration reg)
        {
            var lease = new SleepProxyLease
            {
                Duration = options.DetermineLeaseDuration(reg.RequestedLease)
            };

            if (Context.FindHostContextBy(reg.PhysicalAddress) is not NetworkHostContext ctxHost)
            {
                ctxHost = CreateHost(reg);

                lease.AddInstanceForDisposal(ctxHost);
            }

            foreach (var adr in reg.IPAddresses.Where(adr => adr.Key.AddressFamily.ShouldDiscover(ctxHost.Auto)))
            {
                ctxHost.Host.AddAddress(adr.Key, adr.Value); // TODO was passiert mit den Addressen, wenn der Host aufwacht?
            }

            if (ctxHost.Auto.HasFlag(AutoDiscoveryType.Service))
            {
                foreach (var serviceInfo in reg.Services)
                {
                    if (ctxHost.FindServiceContextBy(serviceInfo.TransportService) is NetworkHostServiceContext ctxService)
                    {
                        Logger.LogWarning("Already watching service at {Port} for {Host}", serviceInfo.TransportService, ctxHost.Host.Name);

                        continue;
                    }

                    ctxService = ctxHost.CreateService(serviceInfo);

                    lease.AddInstanceForDisposal(ctxService);
                }
            }
            else if (reg.Services.Count > 0)
            {
                Logger.LogWarning("Host {Host} is not configured to discover services.", ctxHost.Host.Name);
            }

            _activeLeases.Add(lease);

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
                hostInfo = new RemotePhysicalHostInfo() { Name = reg.Name };
            }

            hostInfo.AutoDetect = AutoDiscoveryType.IP | AutoDiscoveryType.Host | AutoDiscoveryType.Service;

            hostInfo.HostName = reg.Hostname;
            hostInfo.MAC = reg.PhysicalAddress;

            hostInfo.WakePasswordBytes = reg.Password;

            return Context.CreateDynamicHost([ new TypedParameter(hostInfo.GetType(), hostInfo), .. parameters ]);
        }
    }
}
