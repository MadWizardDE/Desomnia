using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Filter.Rules;
using PacketDotNet;

namespace MadWizard.Desomnia.Network.Filter
{
    public struct PacketFilterOptions
    {
        public bool BlockByDefault;
        public bool NeedsIPTraffic;

        /// <summary>
        /// The host is a placeholder awaiting the registration of its services (via the Sleep Proxy
        /// endpoint): nothing shall wake it, until then. Cleared dynamically as soon as any 
        /// service watch is present (see HostDemandWatch.FilterOptions).
        /// </summary>
        public bool AwaitServices;

        public PacketFilterOptions() { }

        public PacketFilterOptions(AutoDiscoveryType auto)
        {
            if (auto.HasFlag(AutoDiscoveryType.Service))
            {
                BlockByDefault  = true;
                AwaitServices   = true;
            }
        }
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
