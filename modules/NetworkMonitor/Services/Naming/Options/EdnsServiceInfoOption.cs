using Makaretu.Dns;

namespace MadWizard.Desomnia.Network.Naming.Options
{
    /// <summary>
    /// Desomnia-private EDNS0 option (local/experimental-use range) carrying metadata for one service
    /// (identified by its DNS-SD type) that should travel between Desomnia peers but must NOT be
    /// published on the link by a third-party Sleep Proxy: currently just the friendly service name,
    /// which -- were it a TXT attribute -- an Apple Bonjour Sleep Proxy would re-advertise.
    /// </summary>
    public sealed class EdnsServiceInfoOption : EdnsServiceOption
    {
        public EdnsServiceInfoOption() => Type = (EdnsOptionType)0xFEED; // = 65261, IANA local/experimental-use range (65001–65534)

        /// <summary>The friendly service name.</summary>
        public string Name { get; set; } = string.Empty;

        public override void WriteData(WireWriter writer)
        {
            base.WriteData(writer);

            writer.WriteString(Name);
        }

        public override void ReadData(WireReader reader, int length)
        {
            base.ReadData(reader, length);

            Name = reader.ReadString();
        }
    }
}
