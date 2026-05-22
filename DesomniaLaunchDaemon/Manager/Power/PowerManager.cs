using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Power.Manager
{
    public class PowerManager : IPowerManager
    {
        public required ILogger<PowerManager> Logger { private get; init; }

        public event EventHandler? Suspended;
        public event EventHandler? ResumeSuspended;

        public async Task Suspend()
        {
            throw new NotImplementedException("Suspend");
        }

        public async Task Hibernate()
        {
            throw new NotImplementedException("Hibernate");
        }

        public async Task Shutdown(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            throw new NotImplementedException("Shutdown");
        }

        public async Task Reboot(TimeSpan? timeout = null, string? message = null, bool force = false)
        {
            throw new NotImplementedException("Reboot");
        }

        async Task<IPowerRequest> IPowerManager.CreateRequest(string reason)
        {
            throw new NotImplementedException("CreatePowerRequest");
        }

        async IAsyncEnumerator<IPowerRequest> IAsyncEnumerable<IPowerRequest>.GetAsyncEnumerator(CancellationToken token)
        {
            yield break; // TODO: implement PowerRequests enumeration
        }
    }
}
