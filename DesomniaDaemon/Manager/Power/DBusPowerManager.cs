using MadWizard.Desomnia.Daemon.DBus.Interface;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;

namespace MadWizard.Desomnia.Power.Manager
{
    public class DBusPowerManager(InhibitionOperation watchOperation, InhibitionMode watchMode) 
        : IPowerManager, IHostedService
    {
        const string InhibitionName = "Desomnia Sleep Management";

        public required ILogger<DBusPowerManager> Logger { private get; init; }

        public required ILogin1Manager LoginManager { private get; set; }

        public event EventHandler? Suspended;
        public event EventHandler? ResumeSuspended;

        private IDisposable? _sleepSignal;

        async Task IHostedService.StartAsync(CancellationToken token)
        {
            _sleepSignal = await LoginManager.WatchPrepareForSleepAsync(PrepareForSleep);

            Logger.LogTrace("Watching LoginManager signals: {signal}", "PrepareForSleep");

            if (watchOperation != InhibitionOperation.None)
                Logger.LogTrace("Watching Inhibition operations: {operation}", watchOperation);
            if (watchMode != InhibitionMode.None)
                Logger.LogTrace("Watching Inhibition modes: {mode}", watchMode);
        }

        private void PrepareForSleep(bool active)
        {
            Logger.LogDebug("PrepareForSleep = {active}", active);

            if (active)
            {
                Suspended?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ResumeSuspended?.Invoke(this, EventArgs.Empty);
            }
        }

        #region IPowerManager implementation
        public async Task Suspend()
        {
            const string acpi = "S1-S3 (sleep)";

            if (await LoginManager.CanSuspendAsync() == "yes")
            {
                Logger.LogDebug("Requested ACPI state: {acpi}", acpi);

                try
                {
                    await LoginManager.SuspendWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS); // since systemd 249
                }
                catch (DBusErrorReplyException ex) when (ex.ErrorName == "org.freedesktop.DBus.Error.InvalidArgs")
                {
                    await LoginManager.SuspendWithFlagsAsync(0);
                }
            }
            else
            {
                Logger.LogWarning("Requested ACPI state: {acpi} [unsupported]", acpi);
            }
        }

        public async Task Hibernate()
        {
            const string acpi = "S4 (hibernate)";

            if (await LoginManager.CanHibernateAsync() == "yes")
            {
                Logger.LogDebug("Requested ACPI state: {acpi}", acpi);

                try
                {
                    await LoginManager.HibernateWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS); // systemd >= 249
                }
                catch (DBusErrorReplyException ex) when (ex.ErrorName == "org.freedesktop.DBus.Error.InvalidArgs")
                {
                    await LoginManager.HibernateWithFlagsAsync(0);
                }
            }
            else
            {
                Logger.LogWarning("Requested ACPI state: {acpi} [unsupported]", acpi);
            }
        }

        public async Task Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            const string acpi = "S5 (shutdown)";

            Logger.LogDebug("Requested ACPI state: {acpi}", acpi);

            if (message != null)
                Logger.LogInformation("Shutdown message: {message}", message);

            if (!timeout.HasValue)
            {
                try
                {
                    await LoginManager.PowerOffWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS); // systemd >= 249
                }
                catch (DBusErrorReplyException ex) when (ex.ErrorName == "org.freedesktop.DBus.Error.InvalidArgs")
                {
                    await LoginManager.PowerOffWithFlagsAsync(0);
                }
            }
            else
                await LoginManager.ScheduleShutdownAsync("poweroff", ToLogindUsec(timeout.Value));
        }

        public async Task Reboot(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            const string acpi = "S0 (reboot)";

            if (await LoginManager.CanRebootAsync() == "yes")
            {
                Logger.LogDebug("Requested ACPI state: {acpi}", acpi);

                if (message != null)
                    Logger.LogInformation("Reboot message: {message}", message);

                if (!timeout.HasValue)
                {
                    try
                    {
                        await LoginManager.RebootWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS); // systemd >= 249
                    }
                    catch (DBusErrorReplyException ex) when (ex.ErrorName == "org.freedesktop.DBus.Error.InvalidArgs")
                    {
                        await LoginManager.RebootWithFlagsAsync(0);
                    }
                }
                else
                    await LoginManager.ScheduleShutdownAsync("reboot", ToLogindUsec(timeout.Value));
            }
            else
            {
                Logger.LogWarning("Requested ACPI state: {acpi} [unsupported]", acpi);
            }
        }

        async Task<IPowerRequest> IPowerManager.CreateRequest(string reason)
        {
            var request = new InhibitionRequest(InhibitionName, reason,
                InhibitionOperation.Sleep,
                InhibitionMode.Block)
            {
                Handle = await LoginManager.InhibitAsync("sleep", InhibitionName, reason, "block")
            };

            Logger.LogTrace("Created {request}", request);

            return request;
        }

        async IAsyncEnumerator<IPowerRequest> IAsyncEnumerable<IPowerRequest>.GetAsyncEnumerator(CancellationToken token)
        {
            var inhibitors = await LoginManager.ListInhibitorsAsync();

            foreach (var (what, who, why, mode, uid, pid) in inhibitors)
            {
                var inhibition = new InhibitionRequest(who, why,
                        Inhibition.OfOperation(what),
                        Inhibition.OfMode(mode))
                {
                    UID = uid,
                    PID = pid
                };

                if (watchOperation.HasFlag(inhibition.Operation) && watchMode.HasFlag(inhibition.Mode))
                {
                    yield return inhibition;
                }
            }
        }
        #endregion

        async Task IHostedService.StopAsync(CancellationToken token)
        {
            _sleepSignal?.Dispose();

            Logger.LogTrace("Stopped watching.");
        }

        private static ulong ToLogindUsec(TimeSpan timeout)
        {
            // logind ScheduleShutdown takes an absolute realtime timestamp in microseconds.
            var target = DateTimeOffset.UtcNow.Add(timeout);
            return (ulong)(target.ToUnixTimeMilliseconds() * 1000L);
        }
    }
}
