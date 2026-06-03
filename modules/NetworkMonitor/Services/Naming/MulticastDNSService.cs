using ConcurrentCollections;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using ProtocolType = PacketDotNet.ProtocolType;

namespace MadWizard.Desomnia.Network.Naming.MDNS
{
    public interface IMulticastDNSResolver
    {
        void Resolve(MulticastDNSQuery query);
    }

    internal class MulticastDNSService : INetworkService
    {
        /// <summary>The well-known multicast DNS port (RFC 6762).</summary>
        internal static readonly ushort     MulticastPort = 5353;
        /// <summary>The multicast groups mDNS responses are sent to (RFC 6762 §3).</summary>
        internal static readonly IPAddress  MulticastGroupIPv4 = IPAddress.Parse("224.0.0.251");
        internal static readonly IPAddress  MulticastGroupIPv6 = IPAddress.Parse("ff02::fb");

        public required ILogger<MulticastDNSService> Logger { private get; init; }

        public required IEnumerable<IMulticastDNSResolver> Resolvers { private get; init; }

        public required NetworkDevice Device { private get; init; }

        /// <summary>
        /// Queries we are currently holding back on, each waiting to see whether another
        /// responder on the link answers for the same name before we step in ourselves.
        /// </summary>
        readonly ConcurrentHashSet<MulticastDNSQuery> _pendingQueries = [];

        async void INetworkService.ProcessPacket(EthernetPacket packet)
        {
            if (!TryReadMessage(packet, out Message? message))
                return;

            if (message.IsQuery)
            {
                var query = new MulticastDNSQuery(packet, message);

                foreach (var resolver in Resolvers)
                    resolver.Resolve(query);

                _pendingQueries.Add(query);

                try
                {
                    await Task.Delay(query.Delay);
                }
                finally
                {
                    _pendingQueries.TryRemove(query);
                }

                RespondTo(query);
            }

            else if (message.IsResponse)
            {
                SkipPendingResponses(message); // filter out all pending responses, that already got answered
            }
        }

        private void SkipPendingResponses(Message received)
        {
            foreach (ResourceRecord answer in received.Answers)
            {
                foreach (MulticastDNSQuery query in _pendingQueries)
                {
                    query.Response.Answers.RemoveAll(record =>
                    {
                        switch (record)
                        {
                            case PTRRecord recordPTR:
                                return answer is PTRRecord answerPTR && recordPTR.DomainName == answerPTR.DomainName;

                            default:
                                return record.Name == answer.Name;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Sends <paramref name="response"/> back to the link. By default it is multicast to the all-mDNS
        /// group (QM questions, the only ones we answer, ask for that); for testing, <see cref="RespondViaUnicast"/>
        /// switches to a unicast reply straight to the <paramref name="querier"/>.
        /// </summary>
        private void RespondTo(MulticastDNSQuery query)
        {
            if (!query.HasAnswers) // nothing left to say after non-invasive filtering
                return;

            bool ipv4 = query.SourceAddress.AddressFamily == AddressFamily.InterNetwork;

            IPAddress sourceIP = (ipv4 ? Device.IPv4Address : Device.IPv6LinkLocalAddress) 
                ?? throw new NotSupportedException($"Cannot answer mDNS query: device has no {query.SourceAddress.ToFamilyName()} address.");

            IPAddress destinationIP;
            PhysicalAddress destinationMac;
            ushort destinationPort;

            if (query.MayRespondViaUnicast || Debugger.IsAttached)
            {
                destinationIP = query.SourceAddress;
                destinationMac = query.SourcePhysicalAddress;
                destinationPort = query.SourcePort;
            }
            else
            {
                destinationIP = ipv4 ? MulticastGroupIPv4 : MulticastGroupIPv6;
                destinationMac = destinationIP.DeriveLayer2MulticastAddress();
                destinationPort = MulticastPort;
            }

            if (Logger.IsEnabled(LogLevel.Trace))
            foreach (var record in query.Response.Answers)
            {
                Logger.LogTrace("Sending mDNS response: {Record}", record);
            }

            var udp = new UdpPacket(MulticastPort, destinationPort)
            {
                PayloadData = query.Response.ToByteArray()
            };

            IPPacket ip = ipv4
                ? new IPv4Packet(sourceIP, destinationIP) { TimeToLive = 255 }
                : new IPv6Packet(sourceIP, destinationIP) { HopLimit = 255 };

            ip.Protocol = ProtocolType.Udp;
            ip.PayloadPacket = udp;

            var ethernet = new EthernetPacket(Device.PhysicalAddress, destinationMac, ipv4 ? EthernetType.IPv4 : EthernetType.IPv6)
            {
                PayloadPacket = ip
            };

            // Recompute lengths and checksums from the inside out before sending.
            udp.UpdateCalculatedValues();
            udp.UpdateUdpChecksum();
            ip.UpdateCalculatedValues();
            if (ip is IPv4Packet ipv4Packet)
                ipv4Packet.UpdateIPChecksum();

            Device.SendPacket(ethernet);
        }

        /// <summary>
        /// Parses <paramref name="packet"/> as a multicast DNS message (query or received).
        /// </summary>
        /// <returns><c>true</c> when the packet carries a parseable DNS message on the mDNS port.</returns>
        private bool TryReadMessage(EthernetPacket packet, [NotNullWhen(true)] out Message? message)
        {
            message = null;

            if (packet.Extract<UdpPacket>() is UdpPacket udp && udp.DestinationPort == MulticastPort && udp.PayloadData.Length > 0)
            {
                try
                {
                    message = (Message)(new Message().Read(udp.PayloadData));

                    return true;
                }
                catch (Exception e)
                {
                    Logger.LogTrace(e, "Failed to parse a packet on the mDNS port as a DNS message.");

                    return false;
                }
            }

            return false;
        }
    }
}
