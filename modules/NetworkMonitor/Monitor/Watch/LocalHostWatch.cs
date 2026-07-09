using MadWizard.Desomnia.Network.Address;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Naming;
using MadWizard.Desomnia.Network.Naming.Options;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.SleepProxy;
using MadWizard.Desomnia.Network.SleepProxy.Registration;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Watch
{
    public class LocalHostWatch : HostDemandWatch
    {
        protected bool _handoffDone = false;

        internal bool HandoffPending => HandoffOptions.Type != HandoffType.None && _handoffDone == false;

        internal byte SleepProxyRegistrationCycle { get; private set; }

        public override bool IsOnline => true; // the local proxy is always available

        public required AddressMappingService   AddressMapping  { protected get; init; }
        public required MulticastDNSService     MulticastDNS    { private get; init; }

        public required Func<LocalHostWatch, SleepProxyRegistration> CreateSleepProxyRegistration { private get; init; }

        protected override bool ShouldStartRequest(EthernetPacket packet)
        {
            if (base.ShouldStartRequest(packet))
            {
                return !IsOnline || packet.IsIPUnicast(); // if proxy is online, only consider unicast traffic
            }

            return false;
        }

        internal protected virtual async Task HandoffWatch()
        {
            if (HandoffOptions.Type.HasFlag(HandoffType.UnMagicPacket))
            {
                SendUnMagicPacket(Host);
            }

            if (HandoffOptions.Type.HasFlag(HandoffType.SleepProxy))
            {
                var reg = CreateSleepProxyRegistration(this);

                if (reg.IPAddresses.Count  == 0)
                    throw new Exception($"Sleep proxy registration has no IP address.");

                await RegisterWithBestSleepProxy(reg);

                SleepProxyRegistrationCycle++;
            }

            _handoffDone = true;
        }

        internal protected virtual async Task ReclaimWatch()
        {
            if (_handoffDone)
            {
                _handoffDone = false;

                if (HandoffOptions.Type.HasFlag(HandoffType.SleepProxy))
                {
                    // The proxy advertises our records until it sees our Owner option on the wire;
                    // the already-incremented registration cycle marks a new sleep/wake epoch,
                    // so the lease is released immediately.
                    await MulticastDNS.AnnounceOwner(Host, SleepProxyRegistrationCycle);
                }

                Logger.LogDebug("Reclaimed watch for '{Host}'", Host.Name);
            }
        }

        #region SleepProxy
        /// <summary>All sleep proxies currently known on the network, best metrics first.</summary>
        private IEnumerable<NetworkHostService> SelectSleepProxies() =>
            from host in Network
            from service in host.Services.OfType<SleepProxyService>()
            orderby service.Metrics
            select new NetworkHostService(host, service);

        private async Task RegisterWithBestSleepProxy(SleepProxyRegistration reg)
        {
            for (var tries = 0; ; tries++) // one retry spans a whole sweep over the available proxies
            {
                Exception? failure = null;

                foreach (var (proxy, service) in SelectSleepProxies())
                {
                    try
                    {
                        await RegisterWithSleepProxy(proxy, (SleepProxyService)service, reg);

                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Could not register '{Host}' with sleep proxy '{Proxy}'.", Host.Name, proxy.Name);

                        failure = ex;
                    }
                }

                if (failure is null)
                    throw new Exception("No sleep proxy available.");

                if (tries >= HandoffOptions.Retry)
                    throw failure;
            }
        }

        private async Task RegisterWithSleepProxy(NetworkHost proxy, SleepProxyService service, SleepProxyRegistration reg)
        {
            if (!proxy.IPAddresses.Any())
                throw new NotSupportedException($"Sleep proxy '{proxy.Name}' has no IP address.");

            Message dnsRequest = (Message)reg;

            try
            {
                using var cancel = new CancellationTokenSource(HandoffOptions.Timeout);

                byte[] payload = dnsRequest.ToByteArray();

                // Send the identical registration to every known address of the proxy and race them: a single
                // address can fail fast (e.g. no route to an IPv6), so we take the first *successful* response.
                var attempts = proxy.SelectIPAddressesBy(HandoffOptions).Select(async ip =>
                {
                    var endpoint = new IPEndPoint(ip, service.Port);

                    using var client = new UdpClient(ip.AddressFamily);

                    Logger.LogTrace("Try to register '{Host}' with sleep proxy '{Proxy}' via {Endpoint}", Host.Name, proxy.Name, endpoint);

                    await client.SendAsync(payload, endpoint, cancel.Token);

                    return await client.ReceiveAsync(cancel.Token);
                });

                var response = await FirstSuccessful(attempts);

                cancel.Cancel(); // a winner is in: stop and dispose the still-pending attempts

                Logger.LogTrace("Received response from sleep proxy '{Proxy}' via {Endpoint}", proxy.Name, response.RemoteEndPoint);

                var dnsResponse = (Message)new Message().Read(response.Buffer);

                if (dnsResponse.Id != dnsRequest.Id)
                    throw new FormatException($"Sleep proxy '{proxy.Name}' replied with a mismatched message id.");
                if (dnsResponse.Status != MessageStatus.NoError)
                    throw new InvalidOperationException($"Sleep proxy '{proxy.Name}' rejected the registration: {dnsResponse.Status}.");

                TimeSpan? duration = dnsResponse.Options.OfType<EdnsLeaseOption>().FirstOrDefault()?.Duration;

                Logger.LogInformation("Successfully registered '{Host}' with sleep proxy '{Proxy}'; lease granted: {Duration}.", Host.Name, proxy.Name, duration);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Sleep proxy '{proxy.Name}' did not respond within {HandoffOptions.Timeout}.");
            }
        }

        /// <summary>
        /// Awaits the first task to complete <em>successfully</em>, skipping attempts that fail fast (e.g. a UDP
        /// send to an unreachable address). Throws the first failure only once every attempt has failed.
        /// </summary>
        private static async Task<UdpReceiveResult> FirstSuccessful(IEnumerable<Task<UdpReceiveResult>> attempts)
        {
            var pending = attempts.ToList();

            List<Exception> failures = [];

            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending);

                pending.Remove(completed);

                if (completed.IsCompletedSuccessfully)
                    return completed.Result;

                if (completed.Exception?.InnerException is Exception failure) // also observes the fault
                    failures.Add(failure);
            }

            throw failures.FirstOrDefault() ?? new OperationCanceledException();
        }
        #endregion

        #region UnMagicPacket
        private void SendUnMagicPacket(NetworkHost host)
        {
            if (host.PhysicalAddress is PhysicalAddress phy)
            {
                Logger.LogTrace($"Send UnMagic Packet for '{host.Name}' at {phy.ToHexString()}");

                Device.SendPacket(new EthernetPacket(phy, PhysicalAddressExt.Broadcast, EthernetType.WakeOnLan)
                {
                    PayloadPacket = new WakeOnLanPacket(phy)
                });
            }
        }
        #endregion
    }
}