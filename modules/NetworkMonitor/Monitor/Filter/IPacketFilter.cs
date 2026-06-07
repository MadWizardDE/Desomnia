using PacketDotNet;

namespace MadWizard.Desomnia.Network.Filter
{
    public struct PacketFilterOptions
    {
        public bool BlockByDefault;
    }

    public interface IPacketFilter
    {
        bool ShouldFilter(EthernetPacket packet, PacketFilterOptions options = default);
    }

    internal class CompositePacketFilter(IEnumerable<IPacketFilter> filters) : IPacketFilter
    {
        bool IPacketFilter.ShouldFilter(EthernetPacket packet, PacketFilterOptions options)
        {
            foreach (IPacketFilter filter in filters)
            {
                if (filter.ShouldFilter(packet, options))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
