using Makaretu.Dns;

namespace MadWizard.Desomnia.Network.Naming.Options
{
    public abstract class EdnsServiceOption : EdnsOption
    {
        /// <summary>The DNS-SD service type the rules apply to, e.g. "_ssh._tcp.local".</summary>
        public DomainName ServiceDomainName { get; set; } = string.Empty;

        public override void WriteData(WireWriter writer)
        {
            writer.WriteString(ServiceDomainName.ToString());
        }

        public override void ReadData(WireReader reader, int length)
        {
            ServiceDomainName = new DomainName(reader.ReadString());
        }
    }
}