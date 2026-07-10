using MadWizard.Desomnia.Network.Naming.Options;
using Makaretu.Dns;

namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    /// <summary>
    /// The DNS UPDATE messages a Sleep Proxy registration travels as: one message when it fits on
    /// the wire, a burst sharing one message id when it had to be split. A plain holder of standard
    /// DNS messages, used by both sides -- the client builds one (the paging happens in the
    /// <c>MTUSleepProxyMessageSplitter</c> middleware) and enumerates it onto the wire, the
    /// server collects one from received updates (see <see cref="SleepProxyRegistrationBuffer"/>)
    /// and folds it back into the registration via <see cref="Merge"/>.
    /// </summary>
    public class SleepProxyRegistrationMessageBurst : IIEnumerable<Message>
    {
        private readonly List<Message> _messages;

        public SleepProxyRegistrationMessageBurst(params Message[] messages)
        {
            if (messages.Length == 0)
                throw new ArgumentException("A message burst consists of at least one message.", nameof(messages));

            _messages = [.. messages];
        }

        // Properties shared by -- and identical across -- all messages of a burst:

        public ushort Id => _messages[0].Id;

        /// <summary>The Owner option identifying (and waking) the registering host.</summary>
        public EdnsOwnerOption Owner => _messages[0].Options.OfType<EdnsOwnerOption>().FirstOrDefault()
            ?? throw new FormatException("DNS UPDATE without an EDNS0 Owner option");

        /// <summary>The Owner option's sequence number (the host's sleep/wake epoch).</summary>
        public byte Sequence => Owner.Sequence;

        /// <summary>
        /// The announced burst size (<see cref="EdnsPagingOption"/>);
        /// <c>null</c> for clients (like Apple's) that don't announce one.
        /// </summary>
        public byte? ExpectedCount => _messages.SelectMany(message => message.Options).OfType<EdnsPagingOption>().FirstOrDefault()?.Count;

        public int Count => _messages.Count;

        /// <summary>All announced pages have arrived.</summary>
        public bool IsSatisfied  => ExpectedCount is byte expected && Count >= expected;
        /// <summary>Pages of the announced burst are still missing.</summary>
        public bool IsIncomplete => ExpectedCount is byte expected && Count < expected;

        /// <summary>
        /// Adds a message to the burst -- unless its page is already held: duplicate deliveries
        /// (an update raced to several proxy addresses, or observed by both the capture and the
        /// socket inlet) must not count towards the announced page total.
        /// </summary>
        internal void Add(Message message)
        {
            if (PageOf(message) is byte page && _messages.Any(existing => PageOf(existing) == page))
                return;

            _messages.Add(message);
        }

        private static byte? PageOf(Message message) => message.Options.OfType<EdnsPagingOption>().FirstOrDefault()?.Index;

        /// <summary>
        /// Folds the burst back into the single registration it logically forms: questions and
        /// records concatenated (identical records deduplicated), all OPT records carried over --
        /// the <see cref="MakaretuDnsExt"/> Options extension flattens them for the parser.
        /// </summary>
        public Message Merge()
        {
            if (_messages.Count == 1)
                return _messages[0];

            var merged = new Message
            {
                Id = Id,
                Opcode = MessageOperation.Update,
            };

            foreach (var message in _messages)
            {
                foreach (var question in message.Questions)
                    if (!merged.Questions.Any(q => q.Name == question.Name && q.Type == question.Type))
                        merged.Questions.Add(question);

                foreach (var record in message.AuthorityRecords)
                    if (!merged.AuthorityRecords.Any(existing => existing.IsSameRecord(record)))
                        merged.AuthorityRecords.Add(record);

                merged.AdditionalRecords.AddRange(message.AdditionalRecords.OfType<OPTRecord>());
            }

            return merged;
        }

        public IEnumerator<Message> GetEnumerator() => _messages.GetEnumerator();
    }
}
