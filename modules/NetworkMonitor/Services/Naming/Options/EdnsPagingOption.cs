using Makaretu.Dns;

namespace MadWizard.Desomnia.Network.Naming.Options
{
    /// <summary>
    /// Desomnia-proprietary EDNS0 option (local-use code 65003): which page of a Sleep Proxy
    /// registration burst a DNS UPDATE message is, and how many pages the burst consists of.
    /// Desomnia clients stamp it onto every registration (1 / 1 unless the MTU splitter had to
    /// page), so the receiver knows right away whether more messages follow, completes a burst the
    /// moment every page arrived, tells duplicate deliveries of a page apart from new pages, and
    /// detects a lost one. Apple's client doesn't send it and gets a best-effort collection window.
    /// </summary>
    public sealed class EdnsPagingOption : EdnsOption
    {
        public EdnsPagingOption() => Type = (EdnsOptionType)65003; // 65001-65534 Reserved for Local/Experimental Use

        /// <summary>The 1-based index of this message within its burst.</summary>
        public byte Index { get; set; } = 1;

        /// <summary>The total number of messages in the burst.</summary>
        public byte Count { get; set; } = 1;

        public override void ReadData(WireReader reader, int length)
        {
            Index = reader.ReadByte();
            Count = reader.ReadByte();
        }

        public override void WriteData(WireWriter writer)
        {
            writer.WriteByte(Index);
            writer.WriteByte(Count);
        }
    }
}
