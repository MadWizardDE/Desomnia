using Tmds.DBus;

namespace MadWizard.Desomnia.Power.Manager
{
    // D-Bus proxy interface for org.freedesktop.login1.Manager (systemd-logind).
    [DBusInterface("org.freedesktop.login1.Manager")]
    public interface ILogin1Manager : IDBusObject
    {
        const ulong SD_LOGIND_ROOT_CHECK_INHIBITORS = 0x01;
        const ulong SD_LOGIND_SKIP_INHIBITORS       = 0x10;

        Task SuspendWithFlagsAsync      (ulong flags);
        Task HibernateWithFlagsAsync    (ulong flags);

        Task PowerOffWithFlagsAsync     (ulong flags);
        Task RebootWithFlagsAsync       (ulong flags);

        Task ScheduleShutdownAsync(string type, ulong usec);

        // Returns a Unix fd whose lifetime IS the inhibitor lock — close it to release.
        Task<CloseSafeHandle> InhibitAsync(string what, string who, string why, string mode);

        // a(ssssuu): what, who, why, mode, uid, pid
        Task<(string what, string who, string why, string mode, uint uid, uint pid)[]> ListInhibitorsAsync();

        // PrepareForSleep(b active): active=true → about to sleep, active=false → resumed
        Task<IDisposable> WatchPrepareForSleepAsync(Action<bool> handler, Action<Exception>? onError = null);
    }
}
