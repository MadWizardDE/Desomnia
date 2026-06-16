using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Filter.Rules;
using System.Net;

namespace MadWizard.Desomnia.Network.Configuration.Filter
{
    public class HostFilterRuleInfo : IPAddressInfo
    {
        public string? Name { get; init; }

        public FilterRuleType Type { get; set; } = FilterRuleType.MustNot;

        public HostFilterRuleInfo() { }

        internal HostFilterRuleInfo(string name) 
        {
            Name = name;
        }

        internal HostFilterRuleInfo(IPAddress ip) : base(ip) { }

        public bool IsDynamic => !string.IsNullOrWhiteSpace(Name) && !IPAddresses.Any();
    }
}
