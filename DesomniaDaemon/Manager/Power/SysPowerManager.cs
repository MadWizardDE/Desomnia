using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MadWizard.Desomnia.Power.Manager
{
    // Fallback implementation that uses /sys/power/state directly (no D-Bus / logind required).
    // Power inhibitor locks are not available via sysfs — CreateRequest returns a no-op placeholder.
    // Suspended/ResumeSuspended events are only fired around suspend calls made by this daemon;
    // externally-triggered suspends are not observable without D-Bus.
    public class SysPowerManager : IPowerManager
    {
        private const string SysPowerState = "/sys/power/state";

        public required ILogger<SysPowerManager> Logger { private get; init; }

        public event EventHandler? Suspended;
        public event EventHandler? ResumeSuspended;

        private string PowerState
        {
            set
            {
                var supported = !File.ReadAllText(SysPowerState).Contains(value) 
                    ? throw new NotSupportedException($"Power state {value} is not supported by this system.") 
                    : true;

                Logger.LogDebug("/sys/power/state = '{state}'", value);

                Suspended?.Invoke(this, EventArgs.Empty);
                File.WriteAllText(SysPowerState, value); // blocks until resume
                ResumeSuspended?.Invoke(this, EventArgs.Empty);

                Logger.LogDebug("/sys/power/state = null");
            }
        }

        public async Task Suspend()
        {
            Logger.LogDebug("Requested ACPI state: {acpi}", "S1-S3 (sleep)");

            PowerState = "mem";
        }

        public async Task Hibernate()
        {
            Logger.LogDebug("Requested ACPI state: {acpi}", "S4 (hibernate)");

            PowerState = "disk";
        }

        public async Task Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S5 (shutdown)");

            if (message != null)
                Logger.LogInformation("Shutdown message: {message}", message);

            await ExecuteShutdown("-P", timeout);
        }

        public async Task Reboot(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S0 (reboot)");

            if (message != null)
                Logger.LogInformation("Reboot message: {message}", message);

            await ExecuteShutdown("-r", timeout);
        }

        private async Task ExecuteShutdown(string flag, TimeSpan? timeout)
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

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception($"shutdown exited with code {process.ExitCode}.");
        }

        async Task<IPowerRequest> IPowerManager.CreateRequest(string reason)
        {
            throw new NotSupportedException($"Sleep inhibition is not supported by this system.");
        }

        async IAsyncEnumerator<IPowerRequest> IAsyncEnumerable<IPowerRequest>.GetAsyncEnumerator(CancellationToken token)
        {
            yield break;
        }
    }
}
