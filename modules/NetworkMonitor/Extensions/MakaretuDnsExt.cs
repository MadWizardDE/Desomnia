namespace Makaretu.Dns
{
    /// <summary>
    /// Helpers for the multicast-DNS reinterpretation of the 16-bit CLASS field.
    /// </summary>
    /// <remarks>
    /// In multicast DNS (RFC 6762 §5.4 / §10.2) the top bit of the CLASS value is
    /// repurposed: on a question it is the <em>QU</em> ("unicast response requested")
    /// bit, on a resource record it is the cache-flush bit. The version of
    /// Makaretu.Dns.New in use does not expose this bit, and leaves it set in
    /// <see cref="DnsClass"/>, which makes the value compare unequal to e.g.
    /// <see cref="DnsClass.IN"/>. These helpers read the bit and recover the plain class.
    /// </remarks>
    internal static class MakaretuDnsExt
    {
        /// <summary>The top bit of the CLASS field (QU on questions, cache-flush on records).</summary>
        private const ushort MulticastFlag = 0x8000;

        extension(Question question)
        {
            /// <summary>
            /// Whether the QU bit is set, i.e. the sender will also accept a direct
            /// <em>unicast</em> response instead of (only) a multicast one.
            /// </summary>
            public bool QU => ((ushort)question.Class & MulticastFlag) != 0;

            /// <summary>
            /// The actual DNS class with the multicast (QU / cache-flush) bit masked off,
            /// e.g. turns the QU-flagged value back into <see cref="DnsClass.IN"/>.
            /// </summary>
            public DnsClass ClassWithoutMulticastFlag => (DnsClass)((ushort)question.Class & ~MulticastFlag);
        }
    }
}
