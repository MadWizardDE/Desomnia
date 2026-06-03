using Makaretu.Dns;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Naming.MDNS
{
    public class MulticastDNSQuery(EthernetPacket packet, Message query)
    {
        public PhysicalAddress  SourcePhysicalAddress   => field ??= packet.FindSourcePhysicalAddress() ?? throw new ArgumentException("Source MAC missing");
        public IPAddress        SourceAddress           => field ??= packet.FindSourceIPAddress()       ?? throw new ArgumentException("Source IP missing");
        public ushort           SourcePort              => packet.Extract<UdpPacket>()?.SourcePort      ?? throw new ArgumentException("Source port missing");

        public IEnumerable<Question> Questions => query.Questions;

        internal TimeSpan Delay { get; private set; } = TimeSpan.Zero;

        internal Message Response { get; init; } = new Message() { QR = true, AA = true };

        internal bool MayRespondViaUnicast => !Questions.Any(question => !question.QU);

        public void AnswerWith(AddressRecord record, TimeSpan delay = default)
        {
            Response.Answers.Add(record);

            if (delay > Delay)
            {
                Delay = delay;
            }
        }
    }
}
