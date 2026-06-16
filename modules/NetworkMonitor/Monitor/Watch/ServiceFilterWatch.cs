using MadWizard.Desomnia.Network.Filter;
using MadWizard.Desomnia.Network.Filter.Rules;
using MadWizard.Desomnia.Network.Neighborhood;

namespace MadWizard.Desomnia.Network.Watch
{
    public class ServiceFilterWatch(NetworkService service) : NetworkServiceWatch(service)
    {
        public required Lazy<IPacketFilter> Filter { internal get; init; }

        #region Filter rule splitting
        public IEnumerable<PacketFilterRule>    HostFilterRules     => Monitors.OfType<HostDemandWatch>().SelectMany(watch => watch.Filter.Value.Rules);

        public IEnumerable<PacketFilterRule>    ServiceFilterRules  => (IEnumerable<PacketFilterRule>) Filter.Value.Rules.Except(HostFilterRules, ReferenceEqualityComparer.Instance);
        #endregion
    }
}
