using MadWizard.Desomnia.Network.Filter.Rules;
using PacketDotNet;

namespace MadWizard.Desomnia.Network.Filter
{
    public struct PacketFilterOptions
    {
        public bool BlockByDefault;
        public bool NeedsIPTraffic;
    }

    public interface IPacketFilter
    {
        IEnumerable<PacketFilterRule> Rules => [];

        bool ShouldFilter(EthernetPacket packet, PacketFilterOptions options = default);
    }

    internal class CompositePacketFilter(IEnumerable<IPacketFilter> filters) : IPacketFilter
    {
        IEnumerable<PacketFilterRule> IPacketFilter.Rules => filters.SelectMany(x => x.Rules);

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
