using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.SleepProxy;
using MadWizard.Desomnia.Network.SleepProxy.Registration;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Watch
{
    public class LocalHostWatch : HostDemandWatch
    {
        public override bool IsOnline => true; // the local proxy is always available

        public required NetworkSegment Network { private get; init; }

        protected override bool ShouldStartRequest(EthernetPacket packet)
        {
            if (base.ShouldStartRequest(packet))
            {
                return !IsOnline || packet.IsIPUnicast(); // if proxy is online, only consider unicast traffic
            }

            return false;
        }

        internal protected virtual async Task MaybeHandoffWatch()
        {
            if (HandoffOptions.Type != HandoffType.None)
            {
                try
                {
                    if (HandoffOptions.Type.HasFlag(HandoffType.SleepProxy))
                    {
                        if (SelectSleepProxy(out var proxy, out var service))
                        {
                            var registration = CreateSleepProxyRegistration();

                            RegisterWithSleepProxy(proxy, service, registration);
                        }
                    }

                    if (HandoffOptions.Type.HasFlag(HandoffType.UnMagicPacket))
                    {
                        SendUnMagicPacket(Host);
                    }
                }
                catch (Exception ex)
                {
                    if (!HandoffOptions.IsRequired)
                    {
                        Logger.LogError(ex, "Could not handoff watch for '{Host}'.", Host.Name);
                    }
                    else throw;
                }
            }
        }

        #region SleepProxy
        private bool SelectSleepProxy([NotNullWhen(true)] out NetworkHost? proxy, [NotNullWhen(true)] out SleepProxyService? service)
        {
            proxy = null; service = null;
            List<(NetworkHost, SleepProxyService)> availableProxies = [];
            foreach (var host in Network) foreach (var sleep in host.Services.OfType<SleepProxyService>())
                availableProxies.Add((host, sleep));

            if (availableProxies.Count > 0)
            {
                availableProxies.Sort((a, b) =>
                {
                    var ma = a.Item2.Metrics;
                    var mb = b.Item2.Metrics;

                    return ma.CompareTo(mb);
                });

                (proxy, service) = availableProxies[0];

                return true;
            }

            return false;
        }

        private SleepProxyRegistration CreateSleepProxyRegistration()
        {
            var reg = new SleepProxyRegistration(Host)
            {
                Sequence = 1, // TODO analyze and implement

                RequestedLease = HandoffOptions.LeaseDuration,
                Password = HandoffOptions.Password,
            };

            // TODO: add IPs and services

            return reg;
        }

        private void RegisterWithSleepProxy(NetworkHost host, SleepProxyService service, SleepProxyRegistration reg)
        {

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