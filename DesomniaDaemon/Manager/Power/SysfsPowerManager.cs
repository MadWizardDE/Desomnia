using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MadWizard.Desomnia.Power.Manager
{
    // Fallback implementation that uses /sys/power/state directly (no D-Bus / logind required).
    // Power inhibitor locks are not available via sysfs — CreateRequest returns a no-op placeholder.
    // Suspended/ResumeSuspended events are only fired around suspend calls made by this daemon;
    // externally-triggered suspends are not observable without D-Bus.
    public class SysfsPowerManager : IPowerManager
    {
        private const string SysPowerState = "/sys/power/state";

        public required ILogger<SysfsPowerManager> Logger { private get; init; }

        public event EventHandler? Suspended;
        public event EventHandler? ResumeSuspended;

        public void Suspend(bool hibernate = false)
        {
            string state = hibernate ? "disk" : "mem";
            string acpi  = hibernate ? "S4 (hibernate)" : "S1-S3 (sleep)";

            string supported = File.ReadAllText(SysPowerState);
            if (!supported.Contains(state))
                throw new NotSupportedException($"ACPI state {acpi} is not supported by this system.");

            Logger.LogDebug("Requested ACPI state: {acpi}", acpi);

            Suspended?.Invoke(this, EventArgs.Empty);
            File.WriteAllText(SysPowerState, state); // blocks until resume
            ResumeSuspended?.Invoke(this, EventArgs.Empty);
        }

        public void Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S5 (shutdown)");
            if (message != null)
                Logger.LogInformation("Shutdown message: {message}", message);

            ExecuteShutdown("-P", timeout);
        }

        public void Reboot(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S0 (reboot)");
            if (message != null)
                Logger.LogInformation("Reboot message: {message}", message);

            ExecuteShutdown("-r", timeout);
        }

        private void ExecuteShutdown(string flag, TimeSpan? timeout)
        {
            // shutdown(8) uses minutes; round up, minimum 1 minute for any non-zero delay.
            string time = timeout.HasValue
                ? $"+{Math.Max(1, (int)Math.Ceiling(timeout.Value.TotalMinutes))}"
                : "now";

            var startInfo = new ProcessStartInfo("shutdown") { UseShellExecute = false, CreateNoWindow = true };
            startInfo.ArgumentList.Add("--no-wall");
            startInfo.ArgumentList.Add(flag);
            startInfo.ArgumentList.Add(time);

            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new Exception("Failed to start shutdown command.");

            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception($"shutdown exited with code {process.ExitCode}.");
        }

        IPowerRequest IPowerManager.CreateRequest(string reason)
        {
            Logger.LogWarning("Sleep inhibitor requested but sysfs power manager does not support inhibitor locks; ignoring.");
            return new NullPowerRequest(reason);
        }

        IEnumerator<IPowerRequest> IEnumerable<IPowerRequest>.GetEnumerator()
        {
            yield break;
        }

        private sealed class NullPowerRequest(string? reason) : IPowerRequest
        {
            public string Name => "Desomnia";
            public string? Reason => reason;
            public void Dispose() { }
        }
    }
}
