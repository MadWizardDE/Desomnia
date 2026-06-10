using ConcurrentCollections;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Naming
{
    public interface IMulticastDNSResolver
    {
        void Resolve(DNSQuery query);
    }

    internal class MulticastDNSService() : DNSService(MulticastPort)
    {
        /// <summary>The well-known multicast DNS port (RFC 6762).</summary>
        internal static readonly ushort     MulticastPort       = 5353;
        /// <summary>The multicast groups mDNS responses are sent to (RFC 6762 §3).</summary>
        internal static readonly IPAddress  MulticastGroupIPv4  = IPAddress.Parse("224.0.0.251");
        internal static readonly IPAddress  MulticastGroupIPv6  = IPAddress.Parse("ff02::fb");

        public required ILogger<MulticastDNSService> Logger { private get; init; }

        public required Lazy<IEnumerable<IMulticastDNSResolver>> Resolvers { private get; init; }

        public bool ForceUnicast { private get; set; } = false;

        /// <summary>
        /// Queries we are currently holding back on, each waiting to see whether another
        /// responder on the link answers for the same name before we step in ourselves.
        /// </summary>
        readonly ConcurrentHashSet<DNSQuery> _pendingQueries = [];

        protected override void ProcessResponse(Message message)
        {
            SkipPendingResponses(message); // filter out all pending responses, that already got answered
        }

        protected override async void ProcessQuery(DNSQuery query)
        {
            if (query.Request.Opcode == MessageOperation.Query)
            {
                foreach (var resolver in Resolvers.Value)
                    resolver.Resolve(query);

                _pendingQueries.Add(query);

                try
                {
                    await Task.Delay(query.Delay);

                    lock (query) if (query.Response.Answers.Count > 0)
                    {
                        if (Logger.IsEnabled(LogLevel.Trace))
                            foreach (var record in query.Response.Answers)
                                Logger.LogTrace("Sending mDNS response: {Record}", record);

                        RespondTo(query);
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
        }

        internal virtual bool ShouldRespondWithMulticast(DNSQuery query)
        {
            if (query.SourcePort != MulticastPort) // legacy protocol
                return false;

            if (query.Questions.All(question => question.QU)) // client requests unicast
                return false;

            return true;
        }

        protected override void RespondWith(DNSQuery query, EthernetPacket packet)
        {
            if (ForceUnicast || ShouldRespondWithMulticast(query))
            {
                if (packet.PayloadPacket is IPPacket ip)
                {
                    (ip.PayloadPacket as UdpPacket)?.DestinationPort = MulticastPort;

                    (ip as IPv4Packet)?.DestinationAddress = MulticastGroupIPv4;
                    (ip as IPv6Packet)?.DestinationAddress = MulticastGroupIPv6;

                    packet.DestinationHardwareAddress = ip.DestinationAddress.DeriveLayer2MulticastAddress();
                }
            }

            base.RespondWith(query, packet);
        }

        private void SkipPendingResponses(Message received)
        {
            foreach (ResourceRecord answer in received.Answers)
            {
                foreach (DNSQuery query in _pendingQueries) lock (query)
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
    }
}
