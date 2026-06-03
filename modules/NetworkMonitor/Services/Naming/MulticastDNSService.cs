using ConcurrentCollections;
using MadWizard.Desomnia.Network.Naming.Options;
using MadWizard.Desomnia.Network.SleepProxy;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Naming.MDNS
{
    public interface IMulticastDNSResolver
    {
        void Resolve(MulticastDNSQuery query);

        void Update(MulticastDNSUpdate update) { }
    }

    internal class MulticastDNSService : INetworkService
    {
        /// <summary>The well-known multicast DNS port (RFC 6762).</summary>
        internal static readonly ushort     MulticastPort       = 5353;
        /// <summary>The multicast groups mDNS responses are sent to (RFC 6762 §3).</summary>
        internal static readonly IPAddress  MulticastGroupIPv4 = IPAddress.Parse("224.0.0.251");
        internal static readonly IPAddress  MulticastGroupIPv6 = IPAddress.Parse("ff02::fb");

        public required ILogger<MulticastDNSService> Logger { private get; init; }

        public required IEnumerable<IMulticastDNSResolver>  Resolvers { private get; init; }
        public required IEnumerable<ISleepProxyRegistrar>   Registrars { private get; init; }

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
                MulticastDNSQuery query;

                if (message.Opcode == MessageOperation.Update)
                {
                    var update = new MulticastDNSUpdate(packet, message);

                    foreach (var resolver in Resolvers)
                        resolver.Update(update);

                    query = update;
                }
                else
                {
                    query = new MulticastDNSQuery(packet, message);

                    foreach (var resolver in Resolvers)
                        resolver.Resolve(query);
                }

                _pendingQueries.Add(query);

                try
                {
                    await Task.Delay(query.Delay);

                    lock (query) if (query.ShouldRespond() != DNSResponseType.None)
                    {
                        RespondTo(query, Debugger.IsAttached || query.ShouldRespond() == DNSResponseType.Unicast);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not send mDNS response");
                }
                finally
                {
                    _pendingQueries.TryRemove(query);
                }
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
                foreach (MulticastDNSQuery query in _pendingQueries) lock (query)
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
        /// Sends the accumulated answers for <paramref name="query"/> back to the link. The reply is multicast
        /// to the all-mDNS group; but when the query may be answered by sendUnicast (every question has the QU bit
        /// set) or a debugger is attached, it is sent straight back to the querier instead.
        /// </summary>
        private void RespondTo(MulticastDNSQuery query, bool sendUnicast)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
            foreach (var record in query.Response.Answers)
            {
                Logger.LogTrace("Sending mDNS response: {Record}", record);
            }

            UdpPacket udp = new(MulticastPort, MulticastPort)
            {
                PayloadData = query.Response.ToByteArray()
            };

            IPPacket ip = query.SourceIPAddress.AddressFamily == AddressFamily.InterNetwork
                ? new IPv4Packet(Device.IPv4Address,            MulticastGroupIPv4) { TimeToLive  = 255 }
                : new IPv6Packet(Device.IPv6LinkLocalAddress,   MulticastGroupIPv6) { HopLimit    = 255 };

            EthernetPacket eth = new(Device.PhysicalAddress, ip.DestinationAddress.DeriveLayer2MulticastAddress(), EthernetType.None); // EtherType = auto

            if (sendUnicast)
            {
                udp.DestinationPort             = query.SourcePort;
                ip.DestinationAddress           = query.SourceIPAddress;
                eth.DestinationHardwareAddress  = query.SourcePhysicalAddress;
            }

            SendPacket(eth, ip, udp);
        }

        private void SendPacket(EthernetPacket eth, IPPacket ip, UdpPacket udp)
        {
            udp.UpdateCalculatedValues();
            udp.UpdateUdpChecksum();

            ip.PayloadPacket = udp;

            ip.UpdateCalculatedValues();
            if (ip is IPv4Packet ipv4Packet)
                ipv4Packet.UpdateIPChecksum();

            eth.PayloadPacket = ip;

            Device.SendPacket(eth);
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

        /// <summary>Register the sleep-proxy EDNS0 options codes used by the Bonjour Sleep Proxy registration that Makaretu doesn't know about.</summary>
        static MulticastDNSService()
        {
            EdnsOptionRegistry.Register<EdnsLeaseOption>();
            EdnsOptionRegistry.Register<EdnsOwnerOption>();
        }
    }
}
