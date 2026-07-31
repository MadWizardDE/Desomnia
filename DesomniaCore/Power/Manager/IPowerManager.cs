using MadWizard.Desomnia.Power.Source;

namespace MadWizard.Desomnia.Power.Manager
{
    public interface IPowerManager : IAsyncEnumerable<IPowerRequest>
    {
        /// <summary>The power source the system is currently running on.</summary>
        public PowerSource Source { get; }

        public event EventHandler Suspended;
        public event EventHandler ResumeSuspended;

        public Task Suspend();
        public Task Hibernate();

        public Task Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false);
        public Task Reboot  (TimeSpan? timeout = null, string? message = null, bool force = false);

        public Task<IPowerRequest> CreateRequest(PowerRequestType type, string reason);
    }
}
