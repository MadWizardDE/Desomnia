using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Services;
using Makaretu.Dns;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Naming.MDNS
{
    public class MulticastDNSQuery(EthernetPacket packet, Message query)
    {
        // RFC 6762 §10: shared records (PTRs) get a long TTL, host-specific records (SRV/TXT/A) a short one.
        private static readonly TimeSpan SharedRecordTTL = TimeSpan.FromMinutes(75);
        /// <summary>TTL for advertised host address records (RFC 6762 §10 recommends 120 s for host names).</summary>
        private static readonly TimeSpan HostRecordTTL = TimeSpan.FromSeconds(120);

        public PhysicalAddress  SourcePhysicalAddress   => field ??= packet.FindSourcePhysicalAddress() ?? throw new ArgumentException("Source MAC missing");
        public IPAddress        SourceIPAddress         => field ??= packet.FindSourceIPAddress()       ?? throw new ArgumentException("Source IP missing");
        public ushort           SourcePort              => packet.Extract<UdpPacket>()?.SourcePort      ?? throw new ArgumentException("Source port missing");

        public IEnumerable<Question> Questions => query.Questions;

        internal TimeSpan Delay { get; private set; } = TimeSpan.Zero;

        internal Message Response { get; init; } = new Message() { QR = true, AA = true };

        internal bool RespondViaUnicast
        {
            get
            {
                if (SourcePort != MulticastDNSService.MulticastPort) // legacy protocol
                    return true;

                if (Questions.All(question => question.QU)) // client requests unicast
                    return true;

                return false;
            }
        }

        private void AnswerWith(ResourceRecord record, TimeSpan delay = default)
        {
            Response.Answers.Add(record);

            if (delay > Delay)
            {
                Delay = delay;
            }
        }

        public void AnswerWith(NetworkHost host, IPAddress ip, TimeSpan delay = default)
        {
            var record = AddressRecord.Create(host.LocalDomainName, ip);
            record.TTL = HostRecordTTL;

            AnswerWith(record, delay);
        }

        public void AnswerWith(NetworkHost host, TransportNetworkService service, DomainName? instance = default, TimeSpan delay = default)
        {
            instance ??= new([host.Name, .. service.LocalDomainName.Labels]);

            AnswerWith(new PTRRecord
            {
                Name = service.LocalDomainName,
                DomainName = instance,

                TTL = SharedRecordTTL
            }, delay);

            Response.AdditionalRecords.Add(new SRVRecord
            {
                Name = instance, 
                Target = host.LocalDomainName,
                Port = service.Port,

                TTL = HostRecordTTL
            });

            Response.AdditionalRecords.Add(new TXTRecord
            {
                Name = instance,
                Strings = [string.Empty],

                TTL = HostRecordTTL
            });

            foreach (IPAddress ip in host.IPAddresses)
            {
                var record = AddressRecord.Create(host.LocalDomainName, ip);
                record.TTL = HostRecordTTL;

                Response.AdditionalRecords.Add(record);
            }
        }
    }
}
