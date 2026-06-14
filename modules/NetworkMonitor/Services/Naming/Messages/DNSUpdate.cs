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

        internal void AnswerWithLease(TimeSpan duration)
        {
            var opt = new OPTRecord();
            opt.Options.Add(new EdnsLeaseOption { Duration = duration });
            Response.AdditionalRecords.Add(opt);
        }

        internal void AnswerWithError(Exception cause)
        {
            Response.AdditionalRecords.Clear();

            switch (cause)
            {
                case FormatException:
                    Response.Status = MessageStatus.FormatError;
                    break;

                default:
                    Response.Status = MessageStatus.ServerFailure;
                    break;
            }
        }
    }
}
