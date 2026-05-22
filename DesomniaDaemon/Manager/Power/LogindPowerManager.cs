using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace MadWizard.Desomnia.Power.Manager
{
    // D-Bus proxy interface for org.freedesktop.login1.Manager (systemd-logind).
    [DBusInterface("org.freedesktop.login1.Manager")]
    interface ILogin1Manager : IDBusObject
    {
        Task SuspendAsync(bool interactive);
        Task HibernateAsync(bool interactive);
        Task PowerOffAsync(bool interactive);
        Task RebootAsync(bool interactive);
        Task ScheduleShutdownAsync(string type, ulong usec);

        // Returns a Unix fd whose lifetime IS the inhibitor lock — close it to release.
        Task<CloseSafeHandle> InhibitAsync(string what, string who, string why, string mode);

        // a(ssssuu): what, who, why, mode, uid, pid
        Task<(string what, string who, string why, string mode, uint uid, uint pid)[]> ListInhibitorsAsync();

        // PrepareForSleep(b active): active=true → about to sleep, active=false → resumed
        Task<IDisposable> WatchPrepareForSleepAsync(Action<bool> handler, Action<Exception>? onError = null);
    }

    public class LogindPowerManager : IPowerManager, IDisposable
    {
        public required ILogger<LogindPowerManager> Logger { private get; init; }

        private readonly Connection _connection;
        private readonly ILogin1Manager _manager;
        private readonly IDisposable _sleepSignal;

        public LogindPowerManager()
        {
            _connection = new Connection(Address.System);
            _connection.ConnectAsync().GetAwaiter().GetResult();

            _manager = _connection.CreateProxy<ILogin1Manager>(
                "org.freedesktop.login1",
                "/org/freedesktop/login1");

            _sleepSignal = _manager
                .WatchPrepareForSleepAsync(OnPrepareForSleep)
                .GetAwaiter().GetResult();
        }

        public event EventHandler? Suspended;
        public event EventHandler? ResumeSuspended;

        private void OnPrepareForSleep(bool active)
        {
            if (active)
                Suspended?.Invoke(this, EventArgs.Empty);
            else
                ResumeSuspended?.Invoke(this, EventArgs.Empty);
        }

        public void Suspend(bool hibernate = false)
        {
            Logger.LogDebug("Requested ACPI state: {state}", hibernate ? "S4 (hibernate)" : "S1-S3 (sleep)");

            if (hibernate)
                _manager.HibernateAsync(false).GetAwaiter().GetResult();
            else
                _manager.SuspendAsync(false).GetAwaiter().GetResult();
        }

        public void Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S5 (shutdown)");
            if (message != null)
                Logger.LogInformation("Shutdown message: {message}", message);

            if (timeout.HasValue)
                _manager.ScheduleShutdownAsync("poweroff", ToLogindUsec(timeout.Value)).GetAwaiter().GetResult();
            else
                _manager.PowerOffAsync(false).GetAwaiter().GetResult();
        }

        public void Reboot(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S0 (reboot)");
            if (message != null)
                Logger.LogInformation("Reboot message: {message}", message);

            if (timeout.HasValue)
                _manager.ScheduleShutdownAsync("reboot", ToLogindUsec(timeout.Value)).GetAwaiter().GetResult();
            else
                _manager.RebootAsync(false).GetAwaiter().GetResult();
        }

        IPowerRequest IPowerManager.CreateRequest(string reason)
        {
            Logger.LogDebug("Creating sleep inhibitor: {reason}", reason);

            var handle = _manager
                .InhibitAsync("sleep", "Desomnia", reason, "block")
                .GetAwaiter().GetResult();

            return new LogindInhibitorRequest("Desomnia", reason, handle);
        }

        IEnumerator<IPowerRequest> IEnumerable<IPowerRequest>.GetEnumerator()
        {
            var inhibitors = _manager.ListInhibitorsAsync().GetAwaiter().GetResult();

            foreach (var (what, who, why, mode, uid, pid) in inhibitors)
            {
                if (what.Contains("sleep"))
                    yield return new LogindInhibitorRequest(who, why);
            }
        }

        public void Dispose()
        {
            _sleepSignal.Dispose();
            _connection.Dispose();
        }

        private static ulong ToLogindUsec(TimeSpan timeout)
        {
            // logind ScheduleShutdown takes an absolute realtime timestamp in microseconds.
            var target = DateTimeOffset.UtcNow.Add(timeout);
            return (ulong)(target.ToUnixTimeMilliseconds() * 1000L);
        }
    }
}
