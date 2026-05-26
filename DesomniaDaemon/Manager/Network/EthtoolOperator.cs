using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MadWizard.Desomnia.Network.Manager
{
    internal class EthtoolOperator
    {
        public required ILogger<EthtoolOperator> Logger { private get; init; }

        public required NetworkDevice Device { private get; init; }

        public string? this[string settingName]
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
    }
}
