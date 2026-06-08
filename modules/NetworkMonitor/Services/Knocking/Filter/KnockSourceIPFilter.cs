using PacketDotNet;

namespace MadWizard.Desomnia.Network.Knocking.Filter
{
    public class KnockSourceIPFilter : IKnockFilter
    {
        public bool ShouldFilter(IPPacket packet, KnockEvent knock)
        {
            if (!packet.SourceAddress.Equals(knock.SourceAddress))
            {
                return true;
            }

            return false;
        }
    }
}
