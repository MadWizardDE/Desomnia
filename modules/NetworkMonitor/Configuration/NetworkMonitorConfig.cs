using MadWizard.Desomnia.Configuration;
using MadWizard.Desomnia.Network.Configuration.Converter;
using MadWizard.Desomnia.Network.Configuration.Filter;
using MadWizard.Desomnia.Network.Configuration.Hosts;
using MadWizard.Desomnia.Network.Configuration.Knocking;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Knocking.Secrets;
using MadWizard.Desomnia.Network.Manager;
using MadWizard.Desomnia.Network.Naming;
using MadWizard.Desomnia.Network.SleepProxy;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace MadWizard.Desomnia.Network.Configuration
{
    public class NetworkMonitorConfig : LocalHostInfo, IIEnumerable<NetworkHostInfo>
    {
        const long DEFAULT_TIMEOUT_MS = 500;

        internal const string NAMLESS_PREFIX = "NetworkMonitor#";

        // Network-Identification
        public required string  Name            { get; init; }

        public string?          Label           { get => !Name.StartsWith(NAMLESS_PREFIX) ? Name : null; }

        public string?          Interface       { get; set; }
        public IPNetwork?       Network         { get; set; }

        public bool             UseBPF          { get; set; } = true;

        public WakeOnLANMode?   AllowWakeOnLAN  { get; set; } = DefaultWakeOnLANMode();

        // Actions
        public DelayedAction?   OnIdle          { get; set; }
        public DelayedAction?   OnDemand        { get; set; }
        public DelayedAction?   OnConnect       { get; set; }
        public NamedAction?     OnDisconnect    { get; set; }

        // Options
        #region Network :: AutoDiscoveryOptions
        public AutoDiscoveryType    AutoDetect          { get; set; } = AutoDiscoveryType.Nothing;
        internal TimeSpan           AutoTimeout         { get; set; } = TimeSpan.FromSeconds(2);
        internal TimeSpan?          AutoRefresh         { get; set; }
        internal bool               AutoParallel        { get; set; } = true;

        public DiscoveryOptions MakeAutoDiscoveryOptions() => new()
        {
            Timeout = this.AutoTimeout,
            Refresh = this.AutoRefresh,
            Parallel = this.AutoParallel
        };
        #endregion

        #region Network :: SweepOptions
        private TimeSpan            SweepFrequency      { get; set; } = TimeSpan.FromMinutes(1);
        private TimeSpan            SweepDelay          { get; set; } = TimeSpan.FromMinutes(5);

        public SweepOptions MakeSweepOptions() => new()
        {
            Frequency = this.SweepFrequency,
            Delay = this.SweepDelay
        };
        #endregion

        #region Network :: DemandOptions 
        internal DemandSource       DemandSource        { get; set; } = DemandSource.Host;
        internal TimeSpan           DemandTimeout       { get; set; } = TimeSpan.FromSeconds(5);
        internal bool               DemandForward       { get; set; } = true;
        internal int                DemandParallel      { get; set; } = 1;
        #endregion

        #region Network :: AdvertisedOptions 
        internal AdvertiseType      Advertise           { get; set; } = AdvertiseType.Lazy;
        internal bool               AdvertiseHosts      { get; set; } = true;
        internal bool               AdvertiseServices   { get; set; } = false;
        internal bool               AdvertiseIfStopped  { get; set; } = true;
        internal bool               AdvertiseUnicast    { get; set; } = false;

        internal TimeSpan           AdvertiseTimeout    { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);

        internal TimeSpan?          AdvertiseHostTTL    { get; set; }
        internal TimeSpan?          AdvertiseServiceTTL { get; set; }
        #endregion

        #region Network :: HandoffOptions
        internal HandoffType        Handoff             { get; set; } = HandoffType.None;
        internal TimeSpan           HandoffTimeout      { get; set; } = TimeSpan.FromSeconds(5);
        #endregion

        #region Network :: SleepProxyOptions
        internal TimeSpan           SleepProxyMinLease  { get; set; } = TimeSpan.FromMinutes(30);
        internal TimeSpan           SleepProxyMaxLease  { get; set; } = TimeSpan.FromDays(365);
        internal SleepProxyMetrics  SleepProxyMetrics   { get; set; } = SleepProxyMetrics.Best;
        internal ushort             SleepProxyPort      { get; set; } = MulticastDNSService.MulticastPort;

        public SleepProxyOptions MakeSleepProxyOptions() => new()
        {
            MinLeaseDuration = SleepProxyMinLease,
            MaxLeaseDuration = SleepProxyMaxLease
        };
        #endregion

        #region Network :: KnockOptions
        internal string             KnockMethod         { get; set; } = "plain";

        internal ushort             KnockPort           { get; set; } = 62201;
        internal IPProtocol         KnockProtocol       { get; set; } = IPProtocol.UDP;

        internal TimeSpan           KnockDelay          { get; set; } = TimeSpan.FromSeconds(0.5);
        internal TimeSpan?          KnockRepeat         { get; set; }
        internal TimeSpan           KnockTimeout        { get; set; } = TimeSpan.FromSeconds(10);
        // Network ::               KnockSecret
        internal string?            KnockSecret         { get; set; }
        internal string?            KnockSecretAuth     { get; set; }
        internal DigestType         KnockSecretAuthType { get; set; } = default;
        internal string             KnockSecretEncoding { get; set; } = "UTF-8";
        #endregion

        #region Network :: PingOptions
        internal TimeSpan           PingTimeout         { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        internal TimeSpan?          PingFrequency       { get; set; }
        #endregion

        #region Network :: WakeOptions
        internal WakeType           WakeType            { get; set; } = WakeType.Auto;
        internal ushort             WakePort            { get; set; } = 9;
        internal string?            WakePassword        { get; set; }
        internal Encoding           WakeEncoding        { get; set; } = Encoding.ASCII; // TODO: is this a good default, can it be set?
        internal TimeSpan           WakeTimeout         { get; set; } = TimeSpan.FromSeconds(10);
        internal TimeSpan?          WakeRepeat          { get; set; }
        internal bool               WakePing            { get; set; } = false;
        #endregion

        #region Network :: WatchOptions
        internal WatchMode          WatchMode           { get; set; } = WatchMode.Normal;
        internal TimeSpan?          WatchTimeout        { get; set; } = TimeSpan.FromMinutes(1); //= null; // TODO: safety net, but why do we need this?
        internal ushort?            WatchUDPPort        { get; set; } = null;

        public WatchOptions MakeWatchOptions() => new()
        {
            Mode = this.WatchMode,
            Timeout = this.WatchTimeout,
            UDPPorts = this.WatchUDPPort != null ? [this.WatchUDPPort.Value] : [],
        };
        #endregion

        // Hosts
        public LocalHostInfo?                   LocalHost   { get; private set; }
        public IList<RemotePhysicalHostInfo>    RemoteHost  { get; private set; } = [];
        public IList<NetworkSleepProxyInfo>     SleepProxy  { get; private set; } = [];
        public IList<NetworkRouterInfo>         Router      { get; private set; } = [];
        public IList<NetworkHostInfo>           Host        { get; private set; } = [];

        // Host-Ranges
        public IList<NetworkHostRangeInfo>      HostRange           { get; private set; } = [];
        public IList<DynamicHostRangeInfo>      DynamicHostRange    { get; private set; } = [];

        // Filter-Rules (networkwide)
        public EveryHostFilterRuleInfo? EveryHostFilterRule { get; set; }
        public ForeignHostFilterRuleInfo? ForeignHostFilterRule { get; set; }
        public IEnumerable<ServiceFilterRuleInfo> ServiceFilterRules => ServiceFilterRule.Concat(HTTPFilterRule);
        public IList<ServiceFilterRuleInfo> ServiceFilterRule { get; set; } = [];
        public IList<HTTPFilterRuleInfo> HTTPFilterRule { get; set; } = [];
        public PingFilterRuleInfo? PingFilterRule { get; set; }

        private static WakeOnLANMode? DefaultWakeOnLANMode()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
               return WakeOnLANMode.MagicPacket | WakeOnLANMode.Default; // don't replace existing modes
            }

            return null;
        }

        #region Host(-Range) enumeration
        public IEnumerable<NetworkHostRangeInfo> Ranges => HostRange.Concat(DynamicHostRange)
            .Concat(EveryHostFilterRule?.HostRange ?? []).Concat(EveryHostFilterRule?.DynamicHostRange ?? [])
            .Concat(ForeignHostFilterRule?.HostRange ?? []).Concat(ForeignHostFilterRule?.DynamicHostRange ?? []);

        public IEnumerable<NetworkHostInfo> Hosts => Host
            .Concat(SleepProxy)
            .Concat(Ranges.SelectMany(range => range.Host)) // all hosts in all host ranges
            .Concat(EveryHostFilterRule?.Host ?? [])
            .Concat(ForeignHostFilterRule?.Host ?? []);

        /// <returns>All configured hosts, regardless of type.</returns>
        IEnumerator<NetworkHostInfo> IEnumerable<NetworkHostInfo>.GetEnumerator() => Hosts
            .Concat(Router)
            .Concat(RemoteHost)
            .Concat(RemoteHost.SelectMany(r => r.VirtualHost))
            .Concat(LocalHost?.VirtualHost ?? [])
            .Concat(VirtualHost).GetEnumerator();
        #endregion

        static NetworkMonitorConfig() // we want to use native types
        {
            TypeDescriptor.AddAttributes(typeof(PhysicalAddress),   new TypeConverterAttribute(typeof(PhysicalAddressConverter)));
            TypeDescriptor.AddAttributes(typeof(IPAddress),         new TypeConverterAttribute(typeof(IPAddressConverter)));
            TypeDescriptor.AddAttributes(typeof(IPNetwork),         new TypeConverterAttribute(typeof(IPNetworkConverter)));
        }
    }
}
