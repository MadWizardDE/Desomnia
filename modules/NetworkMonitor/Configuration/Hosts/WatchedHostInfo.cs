using MadWizard.Desomnia.Configuration;
using MadWizard.Desomnia.Network.Configuration.Filter;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Configuration.Services;

namespace MadWizard.Desomnia.Network.Configuration.Hosts
{
    public class WatchedHostInfo : NetworkHostInfo
    {
        public TrafficThreshold? MinTraffic { get; set; }

        // Options
        #region         DemandOptions
        DemandSource?   DemandSource        { get; set; }
        TimeSpan?       DemandTimeout       { get; set; }
        bool?           DemandForward       { get; set; }
        int?            DemandParallel      { get; set; }

        public virtual DemandOptions MakeDemandOptions(NetworkMonitorConfig network) => new()
        {
            Source      = DemandSource          ?? network.DemandSource,
            Timeout     = DemandTimeout         ?? network.DemandTimeout,
            Forward     = DemandForward         ?? network.DemandForward,
            Parallel    = DemandParallel        ?? network.DemandParallel,
        };
        #endregion

        #region         AdvertiseOptions
        AdvertiseType?  Advertise           { get; set; }
        TimeSpan?       AdvertiseTimeout    { get; set; }
        bool?           AdvertiseHostname   { get; set; }
        bool?           AdvertiseServices   { get; set; }

        public virtual AdvertiseOptions MakeAdvertiseOptions(NetworkMonitorConfig network) => new()
        {
            Type        = (Advertise            ?? network.Advertise)
                        | (AdvertiseHostname    ?? network.AdvertiseHostname ? AdvertiseType.Hostname : 0)
                        | (AdvertiseServices    ?? network.AdvertiseServices ? AdvertiseType.Services : 0),

            Timeout     = AdvertiseTimeout      ?? network.AdvertiseTimeout,
        };
        #endregion

        // Events
        public NamedAction?     OnServiceDemand { get; set; }
        public NamedAction?     OnDemand        { get; set; } = new NamedAction("wake");
        public DelayedAction?   OnIdle          { get; set; }

        public DelayedAction?   OnStart         { get; set; }
        public DelayedAction?   OnSuspend       { get; set; }
        public DelayedAction?   OnStop          { get; set; }

        public NamedAction?     OnMagicPacket   { get; set; }

        // Services
        public IList<ServiceInfo> Service { get; set; } = [];
        public IList<HTTPServiceInfo> HTTPService { get; set; } = [];
        public IEnumerable<ServiceInfo> Services => Service.Concat(HTTPService);

        // Filter-Rules
        public IList<HostFilterRuleInfo> HostFilterRule { get; set; } = [];
        public IList<HostRangeFilterRuleInfo> HostRangeFilterRule { get; set; } = [];
        public IEnumerable<ServiceFilterRuleInfo> ServiceFilterRules => ServiceFilterRule.Concat(HTTPFilterRule);
        public IList<ServiceFilterRuleInfo> ServiceFilterRule { get; set; } = [];
        public IList<HTTPFilterRuleInfo> HTTPFilterRule { get; set; } = [];
        public PingFilterRuleInfo? PingFilterRule { get; set; }
    }
}
