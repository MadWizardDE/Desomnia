using MadWizard.Desomnia.Network.Services;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Manager
{
    /**
     * On Linux hosts, Wake-on-LAN cannot be enabled persistently.
     * Therefore the daemon will try to enable it each time, before going to sleep.
     */
    internal class WakeOnLANEnabler(EthtoolOperator? ethtool = null) : INetworkService
    {
        public required ILogger<WakeOnLANEnabler> Logger { private get; init; }

        private EthtoolFlags? SupportedFlags
        {
            get => ParseFlags(ethtool?["Supports Wake-on"]);
        }
        private EthtoolFlags? Flags
        {
            get => ParseFlags(ethtool?["Wake-on"]);

            set => ethtool?["wol"] = FlagsToString(value ?? throw new ArgumentNullException());
        }

        private EthtoolFlags? _flagsToReset;

        void INetworkService.Startup()
        {
            if (ethtool is not null)
            {
                Logger.LogDebug("Automatically enabling Wake-on-LAN before suspend");
            }
            else
            {
                Logger.LogWarning("Automatically enabling Wake-on-LAN is not possible ('ethtool' is not installed)");
            }
        }

        void INetworkService.Suspend()
        {
            try
            {
                if (Flags is EthtoolFlags flags && !flags.HasFlag(EthtoolFlags.MagicPacket))
                {
                    if (!SupportedFlags?.HasFlag(EthtoolFlags.MagicPacket) ?? false)
                    {
                        Logger.LogWarning("Wake-on-LAN (by Magic Packet) is not supported.");
                    }
                    else
                    {
                        Flags = flags | EthtoolFlags.MagicPacket;

                        _flagsToReset = flags;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Wake-on-LAN could not be enabled.");
            }
        }

        void INetworkService.Resume()
        {
            if (_flagsToReset is EthtoolFlags flags)
            {
                try
                {
                    Flags |= flags;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Wake-on-LAN could not be resetted.");
                }
                finally
                {
                    _flagsToReset = null;
                }
            }
        }

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
        // Canonical letter order matches ethtool's own output order.
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

        // Parses the string ethtool prints after "Wake-on: " / "Supports Wake-on: ".
        // "d" and empty strings both map to None.
        private static EthtoolFlags? ParseFlags(string? s)
        {
            if (s != null)
            {
                var result = EthtoolFlags.None;

                foreach (char c in s)
                {
                    if (Mapping.TryGetValue(c, out var flag))
                        result |= flag;
                }

                return result;
            }

            return null;
        }

        // Produces the string to pass to "ethtool -s <iface> wol <value>".
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