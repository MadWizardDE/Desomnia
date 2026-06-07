using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Knocking.Secrets;
using System.Net;
using System.Text;

namespace MadWizard.Desomnia.Network.Configuration.Hosts
{
    public class RemoteHostInfo : WatchedHostInfo
    {
        // Options
        #region                 AdvertiseOptions
        internal bool?          AdvertiseIfStopped  { get; set; }

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

        #region                 PingOptions
        internal TimeSpan?      PingTimeout         { get; set; }
        internal TimeSpan?      PingFrequency       { get; set; }

        public PingOptions MakePingOptions(NetworkMonitorConfig network) => new()
        {
            Timeout = PingTimeout ?? network.PingTimeout,
            Frequency = PingFrequency ?? network.PingFrequency
        };
        #endregion

        #region                 WakeOptions
        internal WakeType?      WakeType            { get; set; }
        internal ushort?        WakePort            { get; set; }
        internal string?        WakePassword        { get; set; }
        internal byte[]?        WakePasswordBytes   { get; set; }
        internal Encoding?      WakeEncoding        { get; set; }
        internal TimeSpan?      WakeTimeout         { get; set; }
        internal TimeSpan?      WakeRepeat          { get; set; }
        internal bool?          WakePing            { get; set; }

        internal bool           WakeSilent          { get; set; }

        public WakeOptions MakeWakeOptions(NetworkMonitorConfig network)
        {
            if ((WakePassword ?? network.WakePassword) is string password)
            {
                WakePasswordBytes ??= (WakeEncoding ?? network.WakeEncoding).GetBytes(password);
            }

            return new()
            {
                Type = WakeType ?? network.WakeType,
                Port = WakePort ?? network.WakePort,

                Password = WakePasswordBytes,

                Timeout = WakeTimeout ?? network.WakeTimeout,
                Repeat = WakeRepeat ?? network.WakeRepeat,
                Ping = WakePing ?? network.WakePing,

                Silent = WakeSilent,
            };
        }
        #endregion

        #region                 HandoffOptions
        internal TimeSpan?      HandoffTimeout        { get; set; }

        public virtual HandoffOptions MakeHandoffOptions(NetworkMonitorConfig network) => new()
        {
            Timeout = HandoffTimeout ?? network.HandoffTimeout,
        };
        #endregion
    }
}
