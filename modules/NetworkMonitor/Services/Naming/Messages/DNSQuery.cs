using MadWizard.Desomnia.Network.Datagram;
using MadWizard.Desomnia.Network.Naming.Messages;
using Makaretu.Dns;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Naming
{
    public class DNSQuery(DatagramPacket packet, Message message) : DNSMessage
    {
        /// <summary>The datagram this query arrived as -- and the route its response takes back.</summary>
        internal DatagramPacket Packet => packet;

        public PhysicalAddress  SourcePhysicalAddress   => packet.SourcePhysicalAddress ?? throw new ArgumentException("Source MAC missing");
        public IPAddress        SourceIPAddress         => packet.Source.Address;
        public IPAddress        TargetIPAddress         => packet.Target.Address;
        public ushort           SourcePort              => (ushort)packet.Source.Port;

        public IPEndPoint       SourceEndpoint          => packet.Source;

        public IEnumerable<Question> Questions => message.Questions;

        internal Message Request => message;

        /// <summary>The size of the request on the wire, in bytes (its UDP payload).</summary>
        internal int MessageLength => packet.Payload.Length;

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
