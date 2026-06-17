using Autofac;
using Autofac.Core;
using Autofac.Features.OwnedInstances;
using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Context;
using MadWizard.Desomnia.Network.Reachability;
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

        public required ReachabilityService Reachability { private get; set; }

        public required Func<TimeSpan, byte, Owned<SleepProxyLease>> CreateLease { private get; init; }

        readonly ConcurrentDictionary<PhysicalAddress, Owned<SleepProxyLease>> _activeLeases = [];

        public TimeSpan? Register(SleepProxyRegistration reg)
        {
            var duration = options.DetermineLeaseDuration(reg.RequestedLease);

            SleepProxyLease lease;
            if (!_activeLeases.TryGetValue(reg.PrimaryAddress, out var owned))
            {
                lease = (owned = CreateLease(duration, reg.Sequence)).Value;
            }
            else if ((lease = owned.Value).Sequence < reg.Sequence)
            {
                // TODO check registration?

                lease.GrantedUntil = DateTime.Now + duration;

                return lease.Duration;
            }
            else
            {
                return null; // we already processed this registration
            }

            Logger.LogDebug("Attempt to register '{Name}' at {PhysicalAddress} with {ServiceCount} service(s) and {AddressCount} address(es)...",
                reg.Name, reg.PrimaryAddress.ToHexString(), reg.Services.Count, reg.IPAddresses.Count);

            try
            {
                if ((Context.FindHostContextBy(reg.PrimaryAddress) ?? Context.FindHostContextBy(reg.Name)) is not NetworkHostContext ctxHost)
                {
                    ctxHost = CreateHost(reg);

                    lease.AddInstanceForDisposal(ctxHost);
                }

                using var scope = Logger.BeginHostScope(ctxHost.Host);

                if (ctxHost.Watch is not RemoteHostWatch remote)
                    throw new NotSupportedException("Service registration is only supported for watched remote hosts.");

                remote.LastSeen = DateTime.Now;

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

                var filterHosts = CreateFilterHosts();

                Task.Run(async () => // this is time consuming and not relevant for the DNS response, so let's decouple it
                {
                    if (ctxHost.Auto.HasFlag(AutoDiscoveryType.MAC) && ctxHost.Host.PhysicalAddress is null)
                    {
                        ctxHost.Host.PhysicalAddress = reg.PrimaryAddress;

                        lease.AddInstanceForDisposal(new SleepProxyPhysicalAddressLease(ctxHost.Host, reg.PrimaryAddress));

                        Logger.LogHostPhysicalAddressChanged(ctxHost.Host, reg.PrimaryAddress);
                    }

                    foreach (var adr in reg.IPAddresses.Where(adr => adr.Key.AddressFamily.ShouldDiscover(ctxHost.Auto)))
                    {
                        if (ctxHost.Host.AddAddress(adr.Key, adr.Value))
                        {
                            lease.AddInstanceForDisposal(new SleepProxyAddressLease(Logger, ctxHost.Host, adr.Key));

                            Logger.LogHostAddressAdded(ctxHost.Host, adr.Key);
                        }
                    }

                    foreach (var host in filterHosts)
                    {
                        await host.DiscoverAddresses();
                    }

                }).ContinueWith(t => FinishRegistration(remote, lease));
            }
            catch (Exception)
            {
                owned.Dispose(); throw;
            }

            _activeLeases[reg.PrimaryAddress] = owned;

            return duration;
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

            return Context.CreateHost([ new TypedParameter(hostInfo.GetType(), hostInfo), .. parameters ]);
        }

        private IEnumerable<NetworkHostContext> CreateFilterHosts() // the remote host may register dynamic host filters
        {
            using (ExecutionContext.SuppressFlow()) // we need to establish a new logging context
            {
                return Task.Run(() => Context.CreateDynamicFilterHosts().ToList()).Result;
            }
        }

        #region Lease start/end validation
        private async Task FinishRegistration(RemoteHostWatch watch, SleepProxyLease lease)
        {
            using var scope = Logger.BeginHostScope(watch.Host);

            lease.Ended += async (sender, args) =>
            {
                if (_activeLeases.Remove(watch.Host.PhysicalAddress!, out var owned))
                {
                    using var scope = Logger.BeginHostScope(watch.Host);

                    if (args.HasExpired)
                    {
                        await TryToExpireLeaseGracefully(watch);
                    }

                    Logger.LogDebug("Lease for '{Host}' is going to end; releasing hold resources...", watch.Host.Name);

                    using (await Context.Network.Mutex.LockAsync())
                    {
                        owned.Dispose();
                    }

                    Logger.LogDebug("Lease for '{Host}' has ended", watch.Host.Name);
                }
            };

            async Task stop(Event @event) => lease.Stop();

            try
            {
                if (await watch.ValidateHandoff())
                {
                    lease.Ended += (sender, args) => watch.Started -= stop;

                    Logger.LogDebug("Handoff from {Host} successful; lease granted until: {GrantedUntil}", watch.Host.Name, lease.GrantedUntil);

                    watch.Started += stop;
                    
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not validate handoff from {Host}.", watch.Host.Name); 
            }

            lease.Stop();
        }

        private async Task TryToExpireLeaseGracefully(RemoteHostWatch watch)
        {
            switch (options.ExpireLease)
            {
                case LeaseExpireAction.None:
                    return;

                case LeaseExpireAction.Wake when !await Reachability.Test(watch):
                    Logger.LogDebug("Lease for '{Host}' is going to end, but the remote host is not responding; trying to wake...", watch.Host.Name);

                    try
                    {
                        await watch.WakeUp();
                    }
                    catch (HostTimeoutException ex)
                    {
                        Logger.LogWarning("Remote host '{Host}' didn't wake up after {Timeout} s",
                            watch.Host.Name, Math.Ceiling(ex.Timeout.TotalSeconds));
                    }
                    break;
            }
        }
        #endregion
    }
}
