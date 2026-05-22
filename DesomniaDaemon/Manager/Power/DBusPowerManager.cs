using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tmds.DBus;

namespace MadWizard.Desomnia.Power.Manager
{
    public class DBusPowerManager : IPowerManager, IHostedService, IDisposable
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
            Logger.LogTrace("Start watching signal: PrepareForSleep");

            _sleepSignal = await LoginManager.WatchPrepareForSleepAsync(PrepareForSleep);
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

        public async Task Hibernate()
        {
            Logger.LogDebug("Requested ACPI state: {state}", "S4 (hibernate)");

            await LoginManager.HibernateAsync(false);
        }


        public async Task Suspend()
        {
            Logger.LogDebug("Requested ACPI state: {state}", "S1-S3 (sleep)");

            await LoginManager.SuspendAsync(false);
        }

        public async Task Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S5 (shutdown)");

            if (message != null)
                Logger.LogInformation("Shutdown message: {message}", message);

            if (timeout.HasValue)
                await LoginManager.ScheduleShutdownAsync("poweroff", ToLogindUsec(timeout.Value));
            else
                await LoginManager.PowerOffAsync(false);
        }

        public async Task Reboot(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            Logger.LogDebug("Requested ACPI state: S0 (reboot)");

            if (message != null)
                Logger.LogInformation("Reboot message: {message}", message);

            if (timeout.HasValue)
                await LoginManager.ScheduleShutdownAsync("reboot", ToLogindUsec(timeout.Value));
            else
                await LoginManager.RebootAsync(false);
        }

        async Task<IPowerRequest> IPowerManager.CreateRequest(string reason)
        {
            Logger.LogDebug("Creating sleep inhibitor: {reason}", reason);

            var handle = await LoginManager.InhibitAsync("sleep", "Desomnia", reason, "block");

            return new InhibitionRequest("Desomnia", reason, handle);
        }

        async IAsyncEnumerator<IPowerRequest> IAsyncEnumerable<IPowerRequest>.GetAsyncEnumerator(CancellationToken token)
        {
            var inhibitors = await LoginManager.ListInhibitorsAsync();

            foreach (var (what, who, why, mode, uid, pid) in inhibitors)
            {
                if (what.Contains("sleep"))
                {
                    yield return new InhibitionRequest(who, why);
                }
            }
        }

        async Task IHostedService.StopAsync(CancellationToken token)
        {
            _sleepSignal?.Dispose();

            Logger.LogTrace("Stopped watching signal: PrepareForSleep");
        }

        public void Dispose()
        {
            SystemBusConnection.Dispose();
        }

        private static ulong ToLogindUsec(TimeSpan timeout)
        {
            // logind ScheduleShutdown takes an absolute realtime timestamp in microseconds.
            var target = DateTimeOffset.UtcNow.Add(timeout);
            return (ulong)(target.ToUnixTimeMilliseconds() * 1000L);
        }
    }
}
