using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MadWizard.Desomnia.Network.Manager
{
    internal class EthtoolOperator : IWakeOnLANManager
    {
        public required ILogger<EthtoolOperator> Logger { private get; init; }

        public required NetworkDevice Device { private get; init; }

        WakeOnLANMode IWakeOnLANManager.SupportedModes
        {
            get => ParseModes(this["Supports Wake-on"]) ?? WakeOnLANMode.None;
        }
        WakeOnLANMode IWakeOnLANManager.Modes
        {
            get => ParseModes(this["Wake-on"]) ?? WakeOnLANMode.None;

            set => this["wol"] = ModesToString(value);
        }

        private string? this[string settingName]
        {
            get
            {
                foreach (string line in (Exec(Device.Name)).Split('\n'))
                {
                    int colonIndex = line.IndexOf(':');

                    if (colonIndex < 0)
                        continue;

                    string key = line[..colonIndex].Trim();
                    string value = line[(colonIndex + 1)..].Trim();

                    if (key.Equals(settingName, StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }
                }

                return null;
            }

            set
            {
                Exec($"-s {Device.Name} {settingName} {value}");
            }
        }

        private string Exec(string arguments)
        {
            Logger.LogTrace($"ethtool {arguments}");

            using var process = System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "ethtool",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("ethtool failed to start.");

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();

                throw new InvalidOperationException($"ethtool failed: {error}");
            }

            return process.StandardOutput.ReadToEnd();
        }

        public static bool IsInstalled
        {
            get
            {
                using var process = System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "sh",
                    ArgumentList = { "-c", "command -v ethtool" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });

                if (process is not null)
                {
                    process.WaitForExit();

                    return process.ExitCode == 0;
                }

                return false;
            }
        }

        #region WakeOnLANMode-Mapping
        // Canonical letter order matches ethtool's own output order.
        private static readonly Dictionary<char, WakeOnLANMode> Mapping = new()
        {
            //['d'] = WakeOnLANMode.None,       // d — disabled

            ['p'] = WakeOnLANMode.PHY,          // p — PHY activity
            ['u'] = WakeOnLANMode.Unicast,      // u — unicast message
            ['m'] = WakeOnLANMode.Multicast,    // m — multicast message
            ['b'] = WakeOnLANMode.Broadcast,    // b — broadcast message
            ['a'] = WakeOnLANMode.ARP,          // a — ARP
            ['g'] = WakeOnLANMode.MagicPacket,  // g — magic packet
            ['s'] = WakeOnLANMode.SecureOn,     // s — SecureOn password for magic packet
            ['f'] = WakeOnLANMode.Filter,       // f — filter(s)
        };

        // Parses the string ethtool prints after "Wake-on: " / "Supports Wake-on: ".
        // "d" and empty strings both map to None.
        private static WakeOnLANMode? ParseModes(string? s)
        {
            if (s != null)
            {
                var result = WakeOnLANMode.None;

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
        private static string ModesToString(WakeOnLANMode flags)
        {
            if (flags != WakeOnLANMode.None)
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
