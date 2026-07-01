using MadWizard.Desomnia.Network.Naming.Options;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Naming
{
    internal abstract class DNSService(ushort port, string? realm = null) : INetworkService
    {
        public required ILogger<DNSService> WireLogger { private get; init; }

        public required NetworkDevice Device { protected get; init; }

        public virtual async Task Startup() { }

        void INetworkService.ProcessPacket(EthernetPacket packet)
        {
            using var scope = WireLogger.BeginRealmScope(realm);

            if (packet.SourceHardwareAddress.Equals(Device.PhysicalAddress))
                return; // don't even think about this

            if (!TryReadMessage(packet, out Message? message))
                return;

            ProcessMessage(packet, message);
        }

        protected virtual void ProcessMessage(EthernetPacket packet, Message message)
        {
            try
            {
                if (message.IsQuery)
                {
                    switch (message.Opcode)
                    {
                        case MessageOperation.Query:
                            DNSQuery query = new(packet, message);
                            ProcessQuery(query);
                            break;

                        case MessageOperation.Update:
                            DNSUpdate update = new(packet, message);
                            ProcessUpdate(update);
                            break;
                    }
                }
                else
                {
                    ProcessResponse(message);
                }
            }
            catch (Exception ex)
            {
                WireLogger.LogTrace(ex, "Failed to process a DNS message on port {Port} [{ID}]", port, message.Id);
            }
        }

        protected virtual void ProcessQuery(DNSQuery query)     { }
        protected virtual void ProcessUpdate(DNSUpdate update)  { }
        protected virtual void ProcessResponse(Message message) { }

        /// <summary>
        /// Parses <paramref name="packet"/> as a multicast DNS message (query or received).
        /// </summary>
        /// <returns><c>true</c> when the packet carries a parseable DNS message on the mDNS port.</returns>
        private bool TryReadMessage(EthernetPacket packet, [NotNullWhen(true)] out Message? message)
        {
            message = null;

            if (packet.Extract<UdpPacket>() is UdpPacket udp && udp.DestinationPort == port && udp.PayloadData.Length > 0)
            {
                try
                {
                    message = (Message)(new Message().Read(udp.PayloadData));

                    return true;
                }
                catch (Exception ex)
                {
                    WireLogger.LogTrace(ex, "Failed to parse a packet on port {Port} as a DNS message.", port);

                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Sends the accumulated answers for <paramref name="query"/> back to the link. The reply is multicast
        /// to the all-mDNS group; but when the query may be answered by sendUnicast (every question has the QU bit
        /// set) or a debugger is attached, it is sent straight back to the querier instead.
        /// </summary>
        protected void RespondTo(DNSQuery query)
        {
            using var scope = WireLogger.BeginRealmScope(realm);

            try
            {
                IPPacket ip = query.SourceIPAddress.AddressFamily == AddressFamily.InterNetwork
                    ? new IPv4Packet(query.TargetIPAddress, query.SourceIPAddress) { TimeToLive = 255 }
                    : new IPv6Packet(query.TargetIPAddress, query.SourceIPAddress) { HopLimit = 255 };

                ip.PayloadPacket = new UdpPacket(port, query.SourcePort)
                {
                    PayloadData = query.Response.ToByteArray()
                };

                EthernetPacket packet = new(Device.PhysicalAddress, query.SourcePhysicalAddress, EthernetType.None) // EtherType = auto
                {
                    PayloadPacket = ip
                };

                RespondWith(query, packet);
            }
            catch (Exception ex)
            {
                WireLogger.LogTrace(ex, "Failed to send a DNS response from port {Port} [{ID}]", port, query.Request.Id);
            }
        }

        protected virtual void RespondWith(DNSQuery query, EthernetPacket packet)
        {
            Device.SendPacket(packet, true);
        }

        public virtual async Task Shutdown(NetworkShutdownReason reason) { }

        /// <summary>Register the sleep-proxy EDNS0 options codes used by the Bonjour Sleep Proxy registration that Makaretu doesn't know about.</summary>
        static DNSService()
        {
            EdnsOptionRegistry.Register<EdnsLeaseOption>();
            EdnsOptionRegistry.Register<EdnsOwnerOption>();

            EdnsOptionRegistry.Register<EdnsServiceFilterOption>();
        }
    }
}
