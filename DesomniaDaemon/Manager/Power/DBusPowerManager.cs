using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace MadWizard.Desomnia.Power.Manager
{
    public class DBusPowerManager(InhibitionOperation watchOperation, InhibitionMode watchMode) 
        : IPowerManager, IHostedService, IDisposable
    {
        const string InhibitionName = "Desomnia Sleep Management";

        public required ILogger<DBusPowerManager> Logger { private get; init; }

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
                const string serviceName    = "org.freedesktop.login1";
                const string objectPath     = "/org/freedesktop/login1";

                if (field == null)
                {
                    field = SystemBusConnection.CreateProxy<ILogin1Manager>(serviceName, objectPath);
                }

                return field;
            }
        }

        public event EventHandler? Suspended;
        public event EventHandler? ResumeSuspended;

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

            try
            {
                await LoginManager.SuspendWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS); // since systemd 249
            }
            catch (DBusException ex) when (ex.ErrorName == "org.freedesktop.DBus.Error.InvalidArgs")
            {
                await LoginManager.SuspendWithFlagsAsync(0);
            }
        }

        public async Task Hibernate()
        {
            Logger.LogDebug("Requested ACPI state: {state}", "S4 (hibernate)");

            try
            {
                await LoginManager.HibernateWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS); // since systemd 249
            }
            catch (DBusException ex) when (ex.ErrorName == "org.freedesktop.DBus.Error.InvalidArgs")
            {
                await LoginManager.HibernateWithFlagsAsync(0);
            }
        }

        public async Task Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S5 (shutdown)");

            if (message != null)
                Logger.LogInformation("Shutdown message: {message}", message);

            if (!timeout.HasValue)
            {
                try
                {
                    await LoginManager.PowerOffWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS); // since systemd 249
                }
                catch (DBusException ex) when (ex.ErrorName == "org.freedesktop.DBus.Error.InvalidArgs")
                {
                    await LoginManager.PowerOffWithFlagsAsync(0);
                }
            }
            else
                await LoginManager.ScheduleShutdownAsync("poweroff", ToLogindUsec(timeout.Value));
        }

        public async Task Reboot(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S0 (reboot)");

            if (message != null)
                Logger.LogInformation("Reboot message: {message}", message);

            if (!timeout.HasValue)
            {
                try
                {
                    await LoginManager.RebootWithFlagsAsync(ILogin1Manager.SD_LOGIND_SKIP_INHIBITORS); // since systemd 249
                }
                catch (DBusException ex) when (ex.ErrorName == "org.freedesktop.DBus.Error.InvalidArgs")
                {
                    await LoginManager.RebootWithFlagsAsync(0);
                }
            }
            else
                await LoginManager.ScheduleShutdownAsync("reboot", ToLogindUsec(timeout.Value));
        }

        async Task<IPowerRequest> IPowerManager.CreateRequest(string reason)
        {
            var request = new InhibitionRequest(InhibitionName, reason,
                InhibitionOperation.Sleep,
                InhibitionMode.Block)
            {
                Handle = await LoginManager.InhibitAsync("sleep", InhibitionName, reason, "block")
            };

            Logger.LogTrace("Created inhibitor: {request}", request);

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
