using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Address;
using Makaretu.Dns;
using System.Net;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Naming.MDNS.Resolver
{
    internal class HostnameResolver : IMulticastDNSResolver
    {
        public required NetworkMonitor Monitor { private get; init; }
        public required NetworkSegment Network { private get; init; }

        void IMulticastDNSResolver.Resolve(MulticastDNSQuery query)
        {
            foreach (var question in query.Questions)
            {
                // When the QU bit is set the sender also accepts a direct unicast reply,
                // which a real responder may send straight back to it. We never observe
                // that on the wire, so we cannot tell whether the query gets answered;
                // to stay non-invasive, ignore these questions entirely.
                if (question.QU)
                    continue;

                bool wantIPv4 = question.Type is DnsType.A or DnsType.ANY;
                bool wantIPv6 = question.Type is DnsType.AAAA or DnsType.ANY;

                // Not every host is actively watched, and only watched hosts carry the
                // advertise information we need in order to answer on their behalf.
                if (ResolveHost(question) is NetworkHost host && Monitor[host] is HostDemandWatch watch)
                {
                    if (watch.AdvertiseOptions.Type.HasFlag(AdvertiseType.Hostname)) // should we answer for this host?
                        foreach (IPAddress ip in host.IPAddresses)
                        {
                            // Only advertise statically configured addresses; everything else was
                            // discovered by one of Desomnia's own naming services and isn't ours to claim.
                            if (!host[ip].HasFlags(IPAddressFlags.Static))
                                continue;

                            if (ip.AddressFamily == AddressFamily.InterNetwork && !wantIPv4)
                                continue;
                            if (ip.AddressFamily == AddressFamily.InterNetworkV6 && !wantIPv6)
                                continue;

                            query.AnswerWith(host, ip, watch.AdvertiseOptions.Timeout);
                        }
                }
            }
        }

        /// <summary>
        /// Maps an address <see cref="Question"/> to one of our known hosts, provided the
        /// queried name addresses a host in the local zone.
        /// </summary>
        private NetworkHost? ResolveHost(Question question)
        {
            // Only address lookups are relevant for the first step.
            if (question.Type is not (DnsType.A or DnsType.AAAA or DnsType.ANY))
                return null;

            if (question.Name.Labels.Count > 0)
            {
                string hostname = question.Name.Labels[0];

                // mDNS resolves names within the ".local" pseudo-TLD; also accept the
                // interface's own DNS zone, in case names are advertised there as well.
                if (Network.IsInLocalZone(question.Name.ToString()))
                {
                    // Reduce a fully-qualified name like "nas.local" to its leading label "nas".
                    return Network[hostname, byHostName: true];
                }
            }

            return null;
        }
    }
}
