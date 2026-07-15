using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Daemon.DBus.Interface
{
    // D-Bus interface for org.freedesktop.login1.Manager (systemd-logind).
    // Implemented by Login1Manager over a compile-time generated proxy (see DBus/Introspection).
    // see: https://www.freedesktop.org/software/systemd/man/latest/org.freedesktop.login1.html
    [DBusService("org.freedesktop.login1", "/org/freedesktop/login1")]
    public interface ILogin1Manager
    {
        const ulong SD_LOGIND_ROOT_CHECK_INHIBITORS = 0x01;
        const ulong SD_LOGIND_SKIP_INHIBITORS       = 0x10;

        Task<string> CanRebootAsync();
        Task<string> CanSuspendAsync();
        Task<string> CanHibernateAsync();

        Task SuspendWithFlagsAsync      (ulong flags);
        Task HibernateWithFlagsAsync    (ulong flags);

        Task PowerOffWithFlagsAsync     (ulong flags);
        Task RebootWithFlagsAsync       (ulong flags);

        Task ScheduleShutdownAsync(string type, ulong usec);

        // Returns a Unix fd whose lifetime IS the inhibitor lock — close it to release.
        Task<SafeHandle> InhibitAsync(string what, string who, string why, string mode);

        // a(ssssuu): what, who, why, mode, uid, pid
        Task<(string what, string who, string why, string mode, uint uid, uint pid)[]> ListInhibitorsAsync();

        // PrepareForSleep(b active): active=true → about to Sleep, active=false → resumed
        Task<IDisposable> WatchPrepareForSleepAsync(Action<bool> handler, Action<Exception>? onError = null);
    }
}
