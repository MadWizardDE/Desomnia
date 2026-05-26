using MadWizard.Desomnia.Network.Services;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Manager.Network
{
    /**
     * On Linux hosts, Wake-on-LAN cannot be enabled persistently.
     * Therefore the daemon will try to enable it each time, before going to sleep.
     */
    internal class WakeOnLANEnabler : INetworkService
    {
        public required ILogger<WakeOnLANEnabler> Logger { private get; init; }

        public required EthtoolOperator Ethtool { private get; init; }

        private EthtoolFlags SupportedFlags
        {
            get
            {
                return ParseFlags(Ethtool["Supports Wake-on"]);
            }
        }
        private EthtoolFlags Flags
        {
            get
            {
                return ParseFlags(Ethtool["Wake-on"]);
            }

            set
            {
                Ethtool["wol"] = FlagsToString(value);
            }
        }

        private EthtoolFlags _flagsOnSuspend;

        void INetworkService.Suspend()
        {
            try
            {
                if (!(_flagsOnSuspend = Flags).HasFlag(EthtoolFlags.MagicPacket))
                {
                    if (!SupportedFlags.HasFlag(EthtoolFlags.MagicPacket))
                    {
                        Logger.LogWarning("Wake-on-LAN (by Magic Packet) is not supported.");
                    }
                    else
                    {
                        Flags |= EthtoolFlags.MagicPacket;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Wake-on-LAN (by Magic Packet) could not be enabled.");
            }
        }

        void INetworkService.Resume()
        {
            try
            {
                Flags |= _flagsOnSuspend;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Wake-on-LAN could not be resetted.");
            }
        }

        void INetworkService.Startup() { }  // don't call Resume() here
        void INetworkService.Shutdown() { } // don't call Suspend() here

        [Flags]
        private enum EthtoolFlags
        {
            None        = 0,        // d — disabled

            Phy         = 1 << 0,   // p — PHY activity
            Unicast     = 1 << 1,   // u — unicast message
            Multicast   = 1 << 2,   // m — multicast message
            Broadcast   = 1 << 3,   // b — broadcast message
            Arp         = 1 << 4,   // a — ARP
            MagicPacket = 1 << 5,   // g — magic packet
            SecureOn    = 1 << 6,   // s — SecureOn password for magic packet
            Filter      = 1 << 7,   // f — filter(s)
        }

        #region EthtoolFlags-Mapping
        // Canonical letter order matches Ethtool's own output order.
        private static readonly Dictionary<char, EthtoolFlags> Mapping = new()
        {
            ['p'] = EthtoolFlags.Phy,
            ['u'] = EthtoolFlags.Unicast,
            ['m'] = EthtoolFlags.Multicast,
            ['b'] = EthtoolFlags.Broadcast,
            ['a'] = EthtoolFlags.Arp,
            ['g'] = EthtoolFlags.MagicPacket,
            ['s'] = EthtoolFlags.SecureOn,
            ['f'] = EthtoolFlags.Filter,
        };

        // Parses the string Ethtool prints after "Wake-on: " / "Supports Wake-on: ".
        // "d" and empty strings both map to None.
        private static EthtoolFlags ParseFlags(string? s)
        {
            var result = EthtoolFlags.None;

            if (s != null)
            foreach (char c in s)
            {
                if (Mapping.TryGetValue(c, out var flag))
                    result |= flag;
            }

            return result;
        }

        // Produces the string to pass to "Ethtool -s <iface> wol <value>".
        // None returns "d" (disable).
        private static string FlagsToString(EthtoolFlags flags)
        {
            if (flags != EthtoolFlags.None)
            {
                var sb = new System.Text.StringBuilder(Mapping.Count);
                foreach (var (letter, flag) in Mapping)
                {
                    if (flags.HasFlag(flag)) sb.Append(letter);
                }
                return sb.ToString();
            }

            return "d";
        }
        #endregion
    }
}