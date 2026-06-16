using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Filter.Rules;
using NetTools;

namespace MadWizard.Desomnia.Network.Configuration.Filter
{
    public class HostRangeFilterRuleInfo : IPAddressRangeInfo
    {
        public string? Name { get; init; }

        public FilterRuleType Type { get; set; } = FilterRuleType.MustNot;

        public HostRangeFilterRuleInfo() { }

        internal HostRangeFilterRuleInfo(string name)
        {
            Name = name;
        }

        internal HostRangeFilterRuleInfo(IPAddressRange range) : base(range) { }

        public bool IsDynamic => !string.IsNullOrWhiteSpace(Name) && AddressRange == null;
    }
}
