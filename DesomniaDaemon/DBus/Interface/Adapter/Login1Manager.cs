using System.Runtime.InteropServices;
using Tmds.DBus.Protocol;
using Login1Proxy = MadWizard.Desomnia.Daemon.DBus.Generated.Manager;

namespace MadWizard.Desomnia.Daemon.DBus.Interface.Adapter
{
    // Adapter over the compile-time generated proxy (Tmds.DBus.Generator, AOT-safe).
    // The bus coordinates come from [DBusService] on ILogin1Manager via RegisterDBusService.
    internal sealed class Login1Manager(DBusManager dbus, string serviceName, string objectPath) : ILogin1Manager
    {
        private readonly Login1Proxy proxy = new(dbus.SystemBusConnection, serviceName, objectPath);

        public Task<string> CanRebootAsync()    => proxy.CanRebootAsync();
        public Task<string> CanSuspendAsync()   => proxy.CanSuspendAsync();
        public Task<string> CanHibernateAsync() => proxy.CanHibernateAsync();

        public Task SuspendWithFlagsAsync(ulong flags)      => proxy.SuspendWithFlagsAsync(flags);
        public Task HibernateWithFlagsAsync(ulong flags)    => proxy.HibernateWithFlagsAsync(flags);

        public Task PowerOffWithFlagsAsync(ulong flags)     => proxy.PowerOffWithFlagsAsync(flags);
        public Task RebootWithFlagsAsync(ulong flags)       => proxy.RebootWithFlagsAsync(flags);

        public Task ScheduleShutdownAsync(string type, ulong usec) => proxy.ScheduleShutdownAsync(type, usec);

        public Task<SafeHandle> InhibitAsync(string what, string who, string why, string mode)
            => proxy.InhibitAsync(what, who, why, mode);

        public Task<(string what, string who, string why, string mode, uint uid, uint pid)[]> ListInhibitorsAsync()
            => proxy.ListInhibitorsAsync();

        public async Task<IDisposable> WatchPrepareForSleepAsync(Action<bool> handler, Action<Exception>? onError = null)
        {
            return await proxy.WatchPrepareForSleepAsync((Notification<bool> notification) =>
            {
                if (notification.Exception is not null)
                    onError?.Invoke(notification.Exception);
                else if (notification.HasValue)
                    handler(notification.Value);
            }, ObserverFlags.None);
        }
    }
}
