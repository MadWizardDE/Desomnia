using Makaretu.Dns;

namespace MadWizard.Desomnia.Network.Naming.Options
{
    /// <summary>
    /// EDNS0 Update-RequestedLease option (code 2): how long a dynamic-update registration should be kept alive.
    /// </summary>
    public sealed class EdnsLeaseOption : EdnsOption
    {
        public EdnsLeaseOption() => Type = (EdnsOptionType)2; // draft-sekar-dns-ul

        /// <summary>The requested (in a request) or granted (in a response) lease duration.</summary>
        public TimeSpan     Duration { get; set; }
        /// <summary>The optional second "key lease" some clients include (the 8-byte form).</summary>
        public TimeSpan?    KeyLease { get; set; }

        public override void ReadData(WireReader reader, int length)
        {
            Duration = reader.ReadTimeSpan32();

            if (length >= 8)
                KeyLease = reader.ReadTimeSpan32();
        }

        public override void WriteData(WireWriter writer)
        {
            writer.WriteTimeSpan32(Duration);

            if (KeyLease is TimeSpan keyLease)
                writer.WriteTimeSpan32(keyLease);
        }
    }
}
