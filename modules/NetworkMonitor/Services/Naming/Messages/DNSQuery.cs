using MadWizard.Desomnia.Network.Naming.Messages;
using Makaretu.Dns;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Naming
{
    public class DNSQuery(EthernetPacket packet, Message message) : DNSMessage
    {
        public PhysicalAddress  SourcePhysicalAddress   => field ??= packet.FindSourcePhysicalAddress() ?? throw new ArgumentException("Source MAC missing");
        public IPAddress        SourceIPAddress         => field ??= packet.FindSourceIPAddress()       ?? throw new ArgumentException("Source IP missing");
        public IPAddress        TargetIPAddress         => field ??= packet.FindTargetIPAddress()       ?? throw new ArgumentException("Target IP missing");
        public ushort           SourcePort              => packet.Extract<UdpPacket>()?.SourcePort      ?? throw new ArgumentException("Source port missing");

        public IPEndPoint       SourceEndpoint          => field ??= new UDPEndPoint(SourceIPAddress, SourcePort);

        public IEnumerable<Question> Questions => message.Questions;

        internal Message Request => message;

        /// <summary>
        /// Whether this query came from a legacy one-shot resolver (an ephemeral source port rather than
        /// the mDNS port). Such queriers expect a conventional unicast DNS reply (RFC 6762 §6.7).
        /// </summary>
        internal bool IsLegacy => SourcePort != MulticastDNSService.MulticastPort;

        /// <summary>
        /// Known-answer suppression (RFC 6762 §7.1): drop any answer the querier already lists in its
        /// own answer section with a remaining TTL of more than half the record's true TTL.
        /// </summary>
        internal void SuppressKnownAnswers()
        {
            if (Request.Answers.Count == 0)
                return;

            Response.Answers.RemoveAll(record => Request.Answers.Any(known =>
                known.IsSameRecord(record) && known.TTL.TotalSeconds > record.TTL.TotalSeconds / 2));
        }

        /// <summary>
        /// Reshapes the response as a conventional unicast reply for a legacy one-shot resolver
        /// (RFC 6762 §6.7): mirror the query id, repeat the question, and cap every record's TTL at 10 s.
        /// The cache-flush bit is left clear (see <see cref="ApplyCacheFlush"/>).
        /// </summary>
        internal void MakeLegacy()
        {
            Response.Id = Request.Id;

            foreach (Question question in Request.Questions)
                Response.Questions.Add(question);

            TimeSpan cap = TimeSpan.FromSeconds(10);

            foreach (ResourceRecord record in Response.Answers.Concat(Response.AdditionalRecords))
                if (record.TTL > cap)
                    record.TTL = cap;
        }
    }
}
