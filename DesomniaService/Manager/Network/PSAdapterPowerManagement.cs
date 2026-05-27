using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MadWizard.Desomnia.Network.Manager
{
    internal class PSAdapterPowerManagement : IWakeOnLANManager
    {
        public required ILogger<PSAdapterPowerManagement> Logger { private get; init; }

        public required NetworkDevice Device { private get; init; }

        WakeOnLANMode IWakeOnLANManager.SupportedModes
        {
            get
            {
                var result = WakeOnLANMode.None;

                if (this["WakeOnMagicPacket"] is string magic && magic != "Unsupported")
                    result |= WakeOnLANMode.MagicPacket;

                if (this["WakeOnPattern"] is string pattern && pattern != "Unsupported")
                    result |= CompositeWakeOnLANManager.Pattern;

                return result;
            }
        }

        WakeOnLANMode IWakeOnLANManager.Modes
        {
            get
            {
                var result = WakeOnLANMode.None;

                if (this["WakeOnMagicPacket"] == "Enabled")
                    result |= WakeOnLANMode.MagicPacket;

                if (this["WakeOnPattern"] == "Enabled")
                    result |= CompositeWakeOnLANManager.Pattern;

                return result;
            }

            set
            {
                this["WakeOnMagicPacket"] = value.HasFlag(WakeOnLANMode.MagicPacket) ? "Enabled" : "Disabled";
                this["WakeOnPattern"] = (value & CompositeWakeOnLANManager.Pattern) != WakeOnLANMode.None ? "Enabled" : "Disabled";
            }
        }

        #region PowerShell helper methods
        private string? this[string property]
        {
            get
            {
                foreach (var line in Exec($"Get-NetAdapterPowerManagement -Name {Quote(Device.Interface.Name)} | Format-List {property}").Split('\n'))
                {
                    int colon = line.IndexOf(':');
                    if (colon < 0) continue;

                    var key = line[..colon].Trim();
                    var value = line[(colon + 1)..].Trim();

                    if (key.Equals(property, StringComparison.OrdinalIgnoreCase))
                        return value;
                }

                return null;
            }

            set => Exec($"Set-NetAdapterPowerManagement -Name {Quote(Device.Interface.Name)} -{property} {value}");
        }

        private string Exec(string command)
        {
            Logger.LogTrace("powershell -Command {command}", command);

            using var process = System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                ArgumentList = { "-NonInteractive", "-Command", command },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException("PowerShell failed to start.");

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"PowerShell failed: {error.Trim()}");
            }

            return process.StandardOutput.ReadToEnd();
        }

        private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
        #endregion
    }
}
