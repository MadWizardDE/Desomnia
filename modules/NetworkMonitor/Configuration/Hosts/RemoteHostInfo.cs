using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Knocking.Secrets;
using System.Net;

namespace MadWizard.Desomnia.Network.Configuration.Hosts
{
    public class RemoteHostInfo : WatchedHostInfo
    {
        // Options
        #region     AdvertiseOptions
        bool?       AdvertiseIfStopped  { get; set; }

        public override AdvertiseOptions MakeAdvertiseOptions(NetworkMonitorConfig network)
        {
            var options = base.MakeAdvertiseOptions(network);

            if (network.WatchMode == WatchMode.Promiscuous)
            {
                if (AdvertiseIfStopped ?? network.AdvertiseIfStopped)
                {
                    options = options with { Type = options.Type | AdvertiseType.Stop };
                }
            }
            else
            {
                options = options with { Type = AdvertiseType.Never };
            }

            return options;
        }
        #endregion

        #region                 KnockOptions
        internal string?        KnockMethod         { get; set; }

        internal IPProtocol?    KnockProtocol       { get; set; }
        internal ushort?        KnockPort           { get; set; }

        internal TimeSpan?      KnockDelay          { get; set; }
        internal TimeSpan?      KnockRepeat         { get; set; }
        internal TimeSpan?      KnockTimeout        { get; set; }

        //                      KnockSecret
        internal string?        KnockSecret         { get; set; }
        internal string?        KnockSecretAuth     { get; set; }
        internal DigestType?    KnockSecretAuthType { get; set; }
        internal string?        KnockSecretEncoding { get; set; }
        #endregion

        #region     PingOptions
        TimeSpan?   PingTimeout     { get; set; }
        TimeSpan?   PingFrequency   { get; set; }

        public PingOptions MakePingOptions(NetworkMonitorConfig network) => new()
        {
            Timeout = PingTimeout ?? network.PingTimeout,
            Frequency = PingFrequency ?? network.PingFrequency
        };
        #endregion

        #region     WakeOptions
        WakeType?   WakeType        { get; set; }
        ushort?     WakePort        { get; set; }
        TimeSpan?   WakeTimeout     { get; set; }
        TimeSpan?   WakeRepeat      { get; set; }
        bool?       WakePing        { get; set; }

        bool        WakeSilent      { get; set; }

        public WakeOptions MakeWakeOptions(NetworkMonitorConfig network) => new()
        {
            Type = WakeType ?? network.WakeType,
            Port = WakePort ?? network.WakePort,

            Timeout = WakeTimeout ?? network.WakeTimeout,
            Repeat = WakeRepeat ?? network.WakeRepeat,
            Ping = WakePing ?? network.WakePing,

            Silent = WakeSilent,
        };
        #endregion

        #region         YieldOptions
        TimeSpan?       YieldTimeout { get; set; }

        public virtual YieldOptions MakeYieldOptions(NetworkMonitorConfig network) => new()
        {
            Timeout = YieldTimeout ?? network.YieldTimeout,
        };
        #endregion
    }
}
