namespace MadWizard.Desomnia.Power.Manager
{
    public interface IPowerManager : IAsyncEnumerable<IPowerRequest>
    {
        public event EventHandler Suspended;
        public event EventHandler ResumeSuspended;

        public Task Suspend();
        public Task Hibernate();

        public Task Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false);
        public Task Reboot  (TimeSpan? timeout = null, string? message = null, bool force = false);

        public Task<IPowerRequest> CreateRequest(string reason);
    }
}
