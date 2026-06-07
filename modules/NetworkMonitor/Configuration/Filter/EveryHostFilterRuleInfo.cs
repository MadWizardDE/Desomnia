using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Configuration.Knocking;

namespace MadWizard.Desomnia.Network.Configuration.Filter
{
    public class EveryHostFilterRuleInfo : IPFilterRuleInfo
    {
        public IList<NetworkHostInfo> Host
        {
            get;

            init
            {
                foreach (var host in (field = value))
                {
                    if (!HostFilterRule.Any(rule => rule.IsDynamic && rule.Name == host.Name))
                    {
                        HostFilterRule.Add(new(host.Name));
                    }
                }
            }
        } = [];

        public IList<NetworkHostRangeInfo> HostRange
        {
            get;

            init
            {
                foreach (var range in (field = value))
                {
                    if (!HostRangeFilterRule.Any(rule => rule.IsDynamic && rule.Name == range.Name))
                    {
                        HostRangeFilterRule.Add(new(range.Name));
                    }
                }
            }
        } = [];

        public IList<DynamicHostRangeInfo> DynamicHostRange
        {
            get;

            init
            {
                foreach (var dynamic in (field = value))
                {
                    if (!HostRangeFilterRule.Any(rule => rule.IsDynamic && rule.Name == dynamic.Name))
                    {
                        HostRangeFilterRule.Add(new(dynamic.Name));
                    }
                }
            }
        } = [];
    }
}
