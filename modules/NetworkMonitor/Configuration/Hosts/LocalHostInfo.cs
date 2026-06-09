using MadWizard.Desomnia.Network.Configuration.Filter;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Configuration.Services;

namespace MadWizard.Desomnia.Network.Configuration.Hosts
{
    public class LocalHostInfo
    {
        public TrafficThreshold? MinTraffic { get; set; }

        // Options
        #region         DemandOptions
        DemandSource?   DemandSource        { get; set; }
        TimeSpan?       DemandTimeout       { get; set; }
        int?            DemandParallel      { get; set; }

        public DemandOptions MakeDemandOptions(NetworkMonitorConfig network) => new()
        {
            Source      = DemandSource      ?? network.DemandSource,
            Timeout     = DemandTimeout     ?? network.DemandTimeout,
            Parallel    = DemandParallel    ?? network.DemandParallel,
            Forward     = false
        };
        #endregion

        #region AdvertiseOptions
        public AdvertiseOptions MakeAdvertiseOptions(NetworkMonitorConfig network) => new()
        {
            Type        = AdvertiseType.Never,
            Timeout     = TimeSpan.MaxValue,
        };
        #endregion

        #region         HandoffOptions
        HandoffType?    Handoff         { get; set; }
        TimeSpan?       HandoffTimeout  { get; set; }

        public virtual HandoffOptions MakeHandoffOptions(NetworkMonitorConfig network) => new()
        {
            Type = Handoff ?? network.Handoff,
            Timeout = HandoffTimeout ?? network.HandoffTimeout,
        };
        #endregion

        // Ports
        public IList<ServiceInfo> Service { get; set; } = [];
        public IList<HTTPServiceInfo> HTTPService { get; set; } = [];
        public IEnumerable<ServiceInfo> Services => Service.Concat(HTTPService);

        // Virtual-Hosts
        public IList<LocalVirtualHostInfo> VirtualHost { get; set; } = [];

        // Filter-Rules
        public IList<HostFilterRuleInfo> HostFilterRule { get; set; } = [];
        public IList<HostRangeFilterRuleInfo> HostRangeFilterRule { get; set; } = [];
    }
}
