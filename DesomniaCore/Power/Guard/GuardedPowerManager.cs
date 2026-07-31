using MadWizard.Desomnia.Power.Manager;
using MadWizard.Desomnia.Power.Source;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Power.Guard
{
    internal class GuardedPowerManager(IPowerManager manager, Lazy<IEnumerable<IPowerTransitionGuard>> guards) : IPowerManager
    {
        public required ILogger<GuardedPowerManager> Logger { private get; init; }

        PowerSource IPowerManager.Source => manager.Source;

        event EventHandler IPowerManager.Suspended
        {
            add     { manager.Suspended += value; }
            remove  { manager.Suspended -= value; }
        }
        event EventHandler IPowerManager.ResumeSuspended
        {
            add     { manager.ResumeSuspended += value; }
            remove  { manager.ResumeSuspended -= value; }
        }

        private async Task AwaitGuards(PowerTransition transition)
        {
            foreach (var guard in guards.Value)
            {
                try
                {
                    await guard.BeforeTransition(transition);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new OperationCanceledException($"Power transition '{transition}' aborted", ex);
                }
            }
        }

        #region Guarded Power Transitions
        async Task IPowerManager.Suspend()
        {
            await AwaitGuards(PowerTransition.Suspend);

            await manager.Suspend();
        }

        async Task IPowerManager.Hibernate()
        {
            await AwaitGuards(PowerTransition.Hibernate);

            await manager.Hibernate();
        }

        async Task IPowerManager.Shutdown(TimeSpan? timeout, string? message, bool force)
        {
            await AwaitGuards(PowerTransition.Shutdown);

            await manager.Shutdown(timeout, message, force);
        }

        async Task IPowerManager.Reboot(TimeSpan? timeout, string? message, bool force)
        {
            await AwaitGuards(PowerTransition.Reboot);

            await manager.Reboot(timeout, message, force);
        }
        #endregion

        Task<IPowerRequest> IPowerManager.CreateRequest(PowerRequestType type, string reason) => manager.CreateRequest(type, reason);

        IAsyncEnumerator<IPowerRequest> IAsyncEnumerable<IPowerRequest>.GetAsyncEnumerator(CancellationToken cancellationToken) => manager.GetAsyncEnumerator(cancellationToken);
    }
}
