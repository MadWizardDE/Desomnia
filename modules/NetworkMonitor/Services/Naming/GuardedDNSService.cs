using Makaretu.Dns;
using PacketDotNet;

namespace MadWizard.Desomnia.Network.Naming
{
    /// <summary>
    /// A <see cref="DNSService"/> that silently drops duplicate messages: when the same message (identified by its
    /// id and a fingerprint of its contents) is seen again within <see cref="DeduplicationWindow"/>, it is ignored.
    /// This filters out raced/retransmitted copies of a single request (e.g. one registration sent to several proxy
    /// addresses). Messages with id 0 are never deduplicated, as multicast DNS uses that id for unrelated messages.
    /// </summary>
    internal abstract class GuardedDNSService(ushort port, string? realm = null) : DNSService(port, realm) // TODO move realm to autofac
    {
        private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromSeconds(5);

        private readonly Dictionary<ulong, DateTime> _seen = [];

        protected override void ProcessMessage(EthernetPacket packet, Message message)
        {
            if (message.Id != 0 && IsDuplicate(message))
                return;

            base.ProcessMessage(packet, message);
        }

        private bool IsDuplicate(Message message)
        {
            lock (_seen)
            {
                var now = DateTime.Now;

                // Expired entries first, so a re-appearing message is processed again after the window has passed.
                foreach (var expired in _seen.Where(entry => now - entry.Value >= DeduplicationWindow).ToList())
                    _seen.Remove(expired.Key);

                // Whatever remains is still within the window, so an existing entry means this is a duplicate.
                return !_seen.TryAdd(Hash(message), now);
            }
        }

        /// <summary>A 64-bit FNV-1a hash over the message's wire form, to tell messages apart cheaply.</summary>
        private static ulong Hash(Message message)
        {
            const ulong offset = 14695981039346656037;
            const ulong prime = 1099511628211;

            ulong hash = offset;
            foreach (byte b in message.ToByteArray())
            {
                hash ^= b;
                hash *= prime;
            }

            return hash;
        }
    }
}
