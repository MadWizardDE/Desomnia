using MadWizard.Desomnia.Network.Naming.Options;
using Makaretu.Dns;
using PacketDotNet;

namespace MadWizard.Desomnia.Network.Naming
{
    public class DNSUpdate : DNSQuery
    {
        internal DNSUpdate(EthernetPacket eth, Message message) : base(eth, message)
        {
            Response = new Message { Id = message.Id, QR = true, Opcode = MessageOperation.Update };
        }

        public EdnsOwnerOption? Owner => Options.OfType<EdnsOwnerOption>().FirstOrDefault();
        public EdnsLeaseOption? Lease => Options.OfType<EdnsLeaseOption>().FirstOrDefault();

        internal void GrantLease(TimeSpan duration)
        {
            var opt = new OPTRecord();
            opt.Options.Add(new EdnsLeaseOption { Duration = duration });
            Response.AdditionalRecords.Add(opt);
        }
    }
}
