using MadWizard.Desomnia.Network.Naming.Options;
using Makaretu.Dns;
using PacketDotNet;

namespace MadWizard.Desomnia.Network.Naming.MDNS
{
    public class MulticastDNSUpdate : MulticastDNSQuery
    {
        internal MulticastDNSUpdate(EthernetPacket eth, Message message) : base(eth, message)
        {
            AuthorityRecords = message.AuthorityRecords;

            Response = new Message { Id = message.Id, QR = true, Opcode = MessageOperation.Update };
        }

        public EdnsOwnerOption? Owner => Options.OfType<EdnsOwnerOption>().FirstOrDefault();
        public EdnsLeaseOption? Lease => Options.OfType<EdnsLeaseOption>().FirstOrDefault();

        public IReadOnlyList<ResourceRecord> AuthorityRecords { get; init; }

        internal override DNSResponseType ShouldRespond()
        {
            if (Response.AdditionalRecords.Count > 0)
            {
                return DNSResponseType.Unicast;
            }

            return DNSResponseType.None;
        }

        internal void GrantLease(TimeSpan duration)
        {
            var opt = new OPTRecord();
            opt.Options.Add(new EdnsLeaseOption { Duration = duration });
            Response.AdditionalRecords.Add(opt);
        }
    }
}
