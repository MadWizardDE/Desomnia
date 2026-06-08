using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Events;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Address
{
    public class AddressMappingService(ILocalAddressMapping addresses, ILocalHostMapping? hosts = null) : INetworkService
    {
        public required ILogger<AddressMappingService> Logger { private get; init; }

        public required NetworkDevice   Device  { private get; init; }
        public required NetworkSegment  Network { private get; init; }
        public required NetworkMonitor  Monitor { private get; init; }

        public void Advertise(AddressMapping mapping, EthernetPacket? respondTo = null)
        {
            switch (mapping.IPAddress.AddressFamily)
            {
                case AddressFamily.InterNetwork when respondTo?.PayloadPacket is ArpPacket arp
                    && arp.Operation == ArpOperation.Request && !arp.IsProbe()
                    && arp.TargetProtocolAddress.Equals(mapping.IPAddress):
                    //Logger.LogDebug($"Received ARP request for Options {mapping.Options}");
                    SendARPResponse(mapping.IPAddress, mapping.PhysicalAddress, arp.SenderProtocolAddress, arp.SenderHardwareAddress);
                    break;

                case AddressFamily.InterNetwork:
                    SendARPAnnouncement(mapping.IPAddress, mapping.PhysicalAddress);
                    break;

                case AddressFamily.InterNetworkV6 when respondTo?.Extract<IPv6Packet>() is IPv6Packet ipv6
                    && ipv6.Extract<NdpNeighborSolicitationPacket>() is NdpNeighborSolicitationPacket ndp
                    && !ipv6.SourceAddress.Equals(IPAddress.IPv6Any) && ndp.TargetAddress.Equals(mapping.IPAddress):
                    SendNDPAdvertisement(mapping.IPAddress, mapping.PhysicalAddress, ipv6.SourceAddress, respondTo.FindSourcePhysicalAddress());
                    break;

                case AddressFamily.InterNetworkV6:
                    SendNDPAdvertisement(mapping.IPAddress, mapping.PhysicalAddress);
                    break;

                default:
                    throw new NotSupportedException($"Address family {mapping.IPAddress.AddressFamily} is not supported.");
            }
        }

        #region Manage static address mappings
        private IEnumerable<NetworkHost> EligibleHosts => Monitor.Where(watch => watch is not LocalHostWatch).Select(watch => watch.Host);

        void INetworkService.Startup()
        {
            if (EligibleHosts.Any())
            {
                Logger.LogDebug("Installing static mappings...");

                foreach (var host in EligibleHosts)
                {
                    host.AddressAdded += Host_AddressAdded;
                    host.PhysicalAddressChanged += Host_PhysicalAddressChanged;
                    host.AddressRemoved += Host_AddressRemoved;

                    foreach (var ip in host.IPAddresses)
                    {
                        if (host.PhysicalAddress is PhysicalAddress mac)
                            if (Network.LocalRange.Contains(ip))
                                addresses.Update(ip, mac);

                        // install static hosts mappings, for static IP addresses
                        if (host[ip].HasFlags(IPAddressFlags.Static) && !host.ShouldAddressExpire(ip, out var expires))
                        {
                            if (!host.HostName.Any(char.IsWhiteSpace))
                            {
                                hosts?.Insert(host.HostName, ip);
                            }
                        }
                    }
                }
            }
        }

        void INetworkService.ProcessPacket(EthernetPacket packet)
        {
            // LATER: maybe update static mappings based on received packets?
        }

        void INetworkService.Shutdown()
        {
            if (EligibleHosts.Any())
            {
                Logger.LogDebug("Deleting static mappings...");

                foreach (var host in EligibleHosts)
                {
                    host.AddressAdded -= Host_AddressAdded;
                    host.PhysicalAddressChanged -= Host_PhysicalAddressChanged;
                    host.AddressRemoved -= Host_AddressRemoved;

                    foreach (var ip in host.IPAddresses)
                    {
                        if (host.PhysicalAddress is not null)
                            if (Network.LocalRange.Contains(ip))
                                addresses.Delete(ip);
                    }

                    hosts?.Delete(host.HostName);
                }
            }
        }
        #endregion

        #region NetworkHost lifecycle
        private void Host_AddressAdded(object? sender, AddressEventArgs args)
        {
            if (sender is NetworkHost host && host.PhysicalAddress is PhysicalAddress mac)
            {
                Logger.LogDebug($"Updating static address mappings for host '{host.Name}'...");

                if (Network.LocalRange.Contains(args.IPAddress))
                    addresses.Update(args.IPAddress, mac);
            }
        }

        private void Host_PhysicalAddressChanged(object? sender, PhysicalAddressEventArgs args)
        {
            if (sender is NetworkHost host)
            {
                Logger.LogDebug($"Updating static address mappings for host '{host.Name}'...");

                foreach (var ip in host.IPAddresses)
                {
                    if (Network.LocalRange.Contains(ip))
                    {
                        addresses.Update(ip, args.PhysicalAddress);
                    }
                }
            }
        }

        private void Host_AddressRemoved(object? sender, AddressRemovedEventArgs args)
        {
            if (sender is NetworkHost host && host.PhysicalAddress is not null)
            {
                Logger.LogDebug($"Updating static address mappings for host '{host.Name}'...");

                if (Network.LocalRange.Contains(args.IPAddress))
                    addresses.Delete(args.IPAddress);
            }
        }
        #endregion

        #region ARP/NDP protocol implementation
        private void SendARPAnnouncement(IPAddress ip, PhysicalAddress mac, PhysicalAddress? macTarget = null)
        {
            if (macTarget == null)
            {
                Logger.LogDebug($"Sending ARP announcement <{ip} -> {mac.ToHexString()}>");

                macTarget = PhysicalAddressExt.Broadcast;
            }
            else
            {
                Logger.LogDebug($"Sending ARP announcement <{ip} -> {mac.ToHexString()}> to {macTarget.ToHexString()}");
            }

            //var response = new EthernetPacket(Options.TryParseFormat("F0-E1-D2-C3-B4-A5"), macTarget, EthernetType.Arp)
            var response = new EthernetPacket(Device.PhysicalAddress, macTarget, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request, PhysicalAddressExt.Empty, ip, mac, ip)
            };

            Device.SendPacket(response);
        }

        private void SendARPResponse(IPAddress ip, PhysicalAddress mac, IPAddress ipTarget, PhysicalAddress macTarget)
        {
            if (mac.Equals(macTarget))
            {
                Logger.LogWarning($"Cannot send ARP response <{ip} -> {mac.ToHexString()}> to {ipTarget} (this is me!)");

                return;
            }

            Logger.LogDebug($"Sending ARP response <{ip} -> {mac.ToHexString()}> to {ipTarget}");

            //var response = new EthernetPacket(Options.TryParseFormat("F0-E1-D2-C3-B4-A5"), macTarget, EthernetType.Arp)
            var response = new EthernetPacket(Device.PhysicalAddress, macTarget, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Response, macTarget, ipTarget, mac, ip)
            };

            Device.SendPacket(response);
        }

        private void SendNDPAdvertisement(IPAddress ip, PhysicalAddress mac, IPAddress? ipTarget = null, PhysicalAddress? macTarget = null, bool unsolicited = false)
        {
            if (mac.Equals(macTarget))
            {
                Logger.LogWarning($"Cannot send NDP advertisement <{ip} -> {mac.ToHexString()}> to {ipTarget} (this is me!)");

                return;
            }

            Logger.LogDebug($"Sending NDP advertisement <{ip} -> {mac.ToHexString()}>"
                + (ipTarget != null ? $" to {ipTarget}" : ""));

            NDPFlags flags = NDPFlags.Override;

            if (ipTarget != null && macTarget != null)
            {
                if (unsolicited != true)
                {
                    flags |= NDPFlags.Solicited;
                }
            }

            var ipSource = Device.IPv6LinkLocalAddress;
            ipTarget ??= Device.IPv6LinkLocalMulticastAddress;
            macTarget ??= ipTarget?.DeriveLayer2MulticastAddress();

            var request = new EthernetPacket(Device.PhysicalAddress, macTarget, EthernetType.IPv6)
            {
                PayloadPacket = new IPv6Packet(ipSource, ipTarget).WithNDPNeighborAdvertisement(flags, ip, mac)
            };

            Device.SendPacket(request);
        }
        #endregion
    }
}
