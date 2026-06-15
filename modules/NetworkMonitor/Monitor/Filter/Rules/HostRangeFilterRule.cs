using MadWizard.Desomnia.Network.Neighborhood;
using NetTools;
using System.Net;

namespace MadWizard.Desomnia.Network.Filter.Rules
{
    public abstract class HostRangeFilterRule : HostFilterRule { }

    public class StaticHostRangeFilterRule : HostRangeFilterRule
    {
        public required IPAddressRange Range { get; init; }

        public override bool MatchesAddress(IPAddress? ip = null)
        {
            return ip != null && Range.Contains(ip);
        }
    }

    public class DynamicHostRangeFilterRule(NetworkHostRange range) : HostRangeFilterRule
    {
        public override bool MatchesAddress(IPAddress? ip = null)
        {
            return ip != null && range.Contains(ip);
        }
    }
}
