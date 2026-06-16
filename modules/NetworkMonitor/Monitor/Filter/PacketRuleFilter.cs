using MadWizard.Desomnia.Network.Filter.Rules;
using PacketDotNet;

namespace MadWizard.Desomnia.Network.Filter
{
    internal class PacketRuleFilter : IPacketFilter
    {
        public required IEnumerable<PacketFilterRule> Rules { get; init; }

        public virtual bool ShouldFilter(EthernetPacket packet, PacketFilterOptions options)
        {
            options.BlockByDefault |= Rules.Any(rule => rule.Type == FilterRuleType.Must);
            options.NeedsIPTraffic |= Rules.Any(rule => rule is IPFilterRule);

            foreach (var rule in Rules)
            {
                if (rule.Matches(packet))
                {
                    if (rule.Type == FilterRuleType.MustNot)
                    {
                        return true;
                    }

                    if (rule.Type == FilterRuleType.Must || rule.Type == FilterRuleType.May)
                    {
                        options.BlockByDefault = false; // no need to find a match anymore
                    }
                }
            }

            if (options.NeedsIPTraffic && !packet.IsIPUnicast())
            {
                throw new IPUnicastNeededException(packet.FindTargetIPAddress()!);
            }

            return options.BlockByDefault;
        }
    }
}
