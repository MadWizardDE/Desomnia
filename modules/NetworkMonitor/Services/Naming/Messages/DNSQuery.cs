using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Services;
using Makaretu.Dns;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Naming
{
    public class DNSQuery(EthernetPacket packet, Message message)
    {
        public PhysicalAddress  SourcePhysicalAddress   => field ??= packet.FindSourcePhysicalAddress() ?? throw new ArgumentException("Source MAC missing");
        public IPAddress        SourceIPAddress         => field ??= packet.FindSourceIPAddress()       ?? throw new ArgumentException("Source IP missing");
        public ushort           SourcePort              => packet.Extract<UdpPacket>()?.SourcePort      ?? throw new ArgumentException("Source port missing");

        public IEnumerable<Question> Questions => message.Questions;

        internal TimeSpan Delay { get; private set; } = TimeSpan.Zero;

        internal Message Request => message;

        internal Message Response { get; init; } = new Message() { QR = true, AA = true };

        private void AnswerWith(ResourceRecord record, TimeSpan delay = default)
        {
            Response.Answers.Add(record);

            if (delay > Delay)
            {
                Delay = delay;
            }
        }

        public void AnswerWith(NetworkHost host, IPAddress ip, AnswerOptions options = default)
        {
            var record = AddressRecord.Create(host.LocalDomainName, ip);
            record.TTL = host[ip].TTL ?? options.HostTTL;

            AnswerWith(record, options.Delay);
        }

        public void AnswerWith(NetworkHost host, TransportNetworkService service, DomainName? instance = default, AnswerOptions options = default)
        {
            instance ??= new([host.Name, .. service.LocalDomainName.Labels]);

            AnswerWith(new PTRRecord
            {
                Name = service.LocalDomainName,
                DomainName = instance,

                TTL = options.ServiceTTL
            }, options.Delay);

            Response.AdditionalRecords.Add(new SRVRecord
            {
                Name = instance,
                Target = host.LocalDomainName,
                Port = service.Port,

                TTL = options.HostTTL
            });

            Response.AdditionalRecords.Add(new TXTRecord
            {
                Name = instance,
                Strings = [string.Empty],

                TTL = options.HostTTL
            });

            foreach (IPAddress ip in host.IPAddresses)
            {
                var record = AddressRecord.Create(host.LocalDomainName, ip);
                record.TTL = host[ip].TTL ?? options.HostTTL;

                Response.AdditionalRecords.Add(record);
            }
        }

        public struct AnswerOptions
        {
            /// <summary>TTL for advertised host address records (RFC 6762 §10 recommends 120 s for host names).</summary>
            public TimeSpan HostTTL { readonly get => field == default ? TimeSpan.FromSeconds(120) : field; set; }
            // RFC 6762 §10: shared records (PTRs) get a long TTL, host-specific records (SRV/TXT/A) a short one.
            public TimeSpan ServiceTTL { readonly get => field == default ? TimeSpan.FromMinutes(75) : field; set; }

            public TimeSpan Delay { get; set; }

            public AnswerOptions(AdvertiseOptions options, bool delay = false)
            {
                if (options.HostTTL is TimeSpan ttlHost)
                    HostTTL = ttlHost;
                if (options.ServiceTTL is TimeSpan ttlService)
                    ServiceTTL = ttlService;

                if (delay)
                {
                    Delay = options.Timeout;
                }
            }
        }
    }
}
