using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace MadWizard.Desomnia.Power.Manager
{
    public class DBusPowerManager(InhibitionOperation watchOperation, InhibitionMode watchMode) 
        : IPowerManager, IHostedService, IDisposable
    {
        public required ILogger<DBusPowerManager> Logger { private get; init; }

        public event EventHandler? Suspended;
        public event EventHandler? ResumeSuspended;

        private Connection      SystemBusConnection
        {
            get
            {
                if (field == null)
                {
                    field = new Connection(Address.System);
                    field.ConnectAsync().GetAwaiter().GetResult();

                    Logger.LogTrace("Connection to D-Bus established.");
                }

                return field;
            }
        }
        private ILogin1Manager  LoginManager
        {
            get
            {
                if (field == null)
                {
                    field = SystemBusConnection.CreateProxy<ILogin1Manager>("org.freedesktop.login1", "/org/freedesktop/login1");
                }

                return field;
            }
        }

        private IDisposable? _sleepSignal;

        async Task IHostedService.StartAsync(CancellationToken token)
        {
            _sleepSignal = await LoginManager.WatchPrepareForSleepAsync(PrepareForSleep);

            Logger.LogTrace("Watching signal: '{Signal}'", "PrepareForSleep");
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

        public async Task Suspend()
        {
            Logger.LogDebug("Requested ACPI state: {state}", "S1-S3 (sleep)");

            await LoginManager.SuspendWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS);
        }

        public async Task Hibernate()
        {
            Logger.LogDebug("Requested ACPI state: {state}", "S4 (hibernate)");

            await LoginManager.HibernateWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS);
        }

        public async Task Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S5 (shutdown)");

            if (message != null)
                Logger.LogInformation("Shutdown message: {message}", message);

            if (!timeout.HasValue)
                await LoginManager.PowerOffWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS);
            else
                await LoginManager.ScheduleShutdownAsync("poweroff", ToLogindUsec(timeout.Value));
        }

        public async Task Reboot(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S0 (reboot)");

            if (message != null)
                Logger.LogInformation("Reboot message: {message}", message);

            if (!timeout.HasValue)
                await LoginManager.RebootWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS);
            else
                await LoginManager.ScheduleShutdownAsync("reboot", ToLogindUsec(timeout.Value));
        }

        async Task<IPowerRequest> IPowerManager.CreateRequest(string reason)
        {
            Logger.LogDebug("Creating sleep inhibitor: {reason}", reason);

            var handle = await LoginManager.InhibitAsync("sleep", "Desomnia", reason, "block");

            return new InhibitionRequest("Desomnia", reason, 
                InhibitionOperation.Sleep, 
                InhibitionMode.Block) 
            {
                Handle = handle
            };
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

        async Task IHostedService.StopAsync(CancellationToken token)
        {
            _sleepSignal?.Dispose();

            Logger.LogTrace("Stopped watching signal: '{Signal}'", "PrepareForSleep");
        }

        public void Dispose()
        {
            SystemBusConnection.Dispose();

            Logger.LogTrace("Disconnected from D-Bus.");
        }

        private static ulong ToLogindUsec(TimeSpan timeout)
        {
            // logind ScheduleShutdown takes an absolute realtime timestamp in microseconds.
            var target = DateTimeOffset.UtcNow.Add(timeout);
            return (ulong)(target.ToUnixTimeMilliseconds() * 1000L);
        }
    }
}
