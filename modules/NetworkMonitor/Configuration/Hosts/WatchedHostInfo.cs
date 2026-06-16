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
        #region                     DemandOptions
        internal DemandSource?      DemandSource        { get; set; }
        internal TimeSpan?          DemandTimeout       { get; set; }
        internal bool?              DemandForward       { get; set; }
        internal int?               DemandParallel      { get; set; }

        public virtual DemandOptions MakeDemandOptions(NetworkMonitorConfig network) => new()
        {
            Source      = DemandSource          ?? network.DemandSource,
            Timeout     = DemandTimeout         ?? network.DemandTimeout,
            Forward     = DemandForward         ?? network.DemandForward,
            Parallel    = DemandParallel        ?? network.DemandParallel,
        };
        #endregion

        #region                     AdvertiseOptions
        internal AdvertiseType?     Advertise           { get; set; }
        internal bool?              AdvertiseHostname   { get; set; }
        internal bool?              AdvertiseServices   { get; set; }

        internal TimeSpan?          AdvertiseTimeout    { get; set; }

        internal TimeSpan?          AdvertiseHostTTL    { get; set; }
        internal TimeSpan?          AdvertiseServiceTTL { get; set; }

        public virtual AdvertiseOptions MakeAdvertiseOptions(NetworkMonitorConfig network) => new()
        {
            Type        = (Advertise            ?? network.Advertise),
                      //| (AdvertiseHostname    ?? network.AdvertiseHosts ? AdvertiseType.Host : 0)
                      //| (AdvertiseServices    ?? network.AdvertiseServices ? AdvertiseType.Service : 0),

            Timeout     = AdvertiseTimeout      ?? network.AdvertiseTimeout,

            HostTTL     = AdvertiseHostTTL      ?? network.AdvertiseHostTTL,
            ServiceTTL  = AdvertiseServiceTTL   ?? network.AdvertiseServiceTTL,

        };
        #endregion

        #region                     HandoffOptions
        internal HandoffType?       Handoff             { get; set; }
        internal TimeSpan?          HandoffTimeout      { get; set; }

        public virtual HandoffOptions MakeHandoffOptions(NetworkMonitorConfig network) => new()
        {
            Type = Handoff ?? network.Handoff,
            Timeout = HandoffTimeout ?? network.HandoffTimeout,
        };
        #endregion

        // Events
        public NamedAction?         OnServiceDemand     { get; set; }
        public NamedAction?         OnDemand            { get; set; } = new NamedAction("wake");
        public DelayedAction?       OnIdle              { get; set; }

        public DelayedAction?       OnStart             { get; set; }
        public DelayedAction?       OnSuspend           { get; set; }
        public DelayedAction?       OnStop              { get; set; }

        public NamedAction?         OnMagicPacket       { get; set; }

        // Services
        public IList<WatchedServiceInfo> Service { get; set; } = [];
        public IList<WatchedHTTPServiceInfo> HTTPService { get; set; } = [];
        public override IEnumerable<WatchedServiceInfo> Services => Service.Concat(HTTPService);

        // Filter-Rules
        public IList<HostFilterRuleInfo> HostFilterRule { get; set; } = [];
        public IList<HostRangeFilterRuleInfo> HostRangeFilterRule { get; set; } = [];
        public IEnumerable<ServiceFilterRuleInfo> ServiceFilterRules => ServiceFilterRule.Concat(HTTPFilterRule);
        public IList<ServiceFilterRuleInfo> ServiceFilterRule { get; set; } = [];
        public IList<HTTPFilterRuleInfo> HTTPFilterRule { get; set; } = [];
        public PingFilterRuleInfo? PingFilterRule { get; set; }
    }
}
