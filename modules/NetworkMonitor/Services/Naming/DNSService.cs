using MadWizard.Desomnia.Network.Naming.Options;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Naming
{
    internal abstract class DNSService(ushort port) : INetworkService
    {
        public required ILogger<DNSService> MessageLogger { private get; init; }

        public required NetworkDevice Device { private get; init; }

        async void INetworkService.ProcessPacket(EthernetPacket packet)
        {
            if (!TryReadMessage(packet, out Message? message))
                return;

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
                catch (Exception e)
                {
                    MessageLogger.LogTrace(e, "Failed to parse a packet on port {Port} as a DNS message.", port);

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
            IPPacket ip = query.SourceIPAddress.AddressFamily == AddressFamily.InterNetwork
                ? new IPv4Packet(Device.IPv4Address, query.SourceIPAddress) { TimeToLive = 255 }
                : new IPv6Packet(Device.IPv6LinkLocalAddress, query.SourceIPAddress) { HopLimit = 255 };

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

        protected virtual void RespondWith(DNSQuery query, EthernetPacket packet)
        {
            Device.SendPacket(packet, true);
        }

        /// <summary>Register the sleep-proxy EDNS0 options codes used by the Bonjour Sleep Proxy registration that Makaretu doesn't know about.</summary>
        static DNSService()
        {
            EdnsOptionRegistry.Register<EdnsLeaseOption>();
            EdnsOptionRegistry.Register<EdnsOwnerOption>();
        }
    }
}
