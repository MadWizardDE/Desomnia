using MadWizard.Desomnia.Pipe.Messages;
using MadWizard.Desomnia.Power.Manager;
using MadWizard.Desomnia.Power.Source;
using MadWizard.Desomnia.Session.Manager;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Service.Bridge
{
    /// <summary>
    /// Decorates the platform <see cref="IPowerManager"/> to route display power requests to the
    /// console session's minion — display requests are session-scoped, so the session-0 service
    /// cannot issue them itself (Windows rejects them with ERROR_NOT_SUPPORTED). The minion holds
    /// the native request inside the interactive session instead.
    ///
    /// Fire-and-forget for now: the (idempotent) hold message is sent without awaiting a reply,
    /// the request is assumed to have succeeded, and disposing the returned
    /// <see cref="SessionMinionDisplayRequest"/> sends the release message. Without a console
    /// session, display requests are meaningless (nobody is at a display) and are ignored via a
    /// no-op request; only when a console session lacks a minion do they fall through to the
    /// platform manager.
    /// </summary>
    internal class BridgedPowerManager(IPowerManager manager, ISessionManager sessions) : IPowerManager
    {
        public required ILogger<BridgedPowerManager> Logger { private get; init; }

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

        Task IPowerManager.Suspend()    => manager.Suspend();
        Task IPowerManager.Hibernate()  => manager.Hibernate();

        Task IPowerManager.Shutdown(TimeSpan? timeout, string? message, bool force) => manager.Shutdown(timeout, message, force);
        Task IPowerManager.Reboot  (TimeSpan? timeout, string? message, bool force) => manager.Reboot(timeout, message, force);

        async Task<IPowerRequest> IPowerManager.CreateRequest(PowerRequestType type, string reason)
        {
            if (type == PowerRequestType.Display)
            {
                // without console session, a display request is meaningless
                if (sessions.ConsoleSession is not Session console)
                    return new IgnoredPowerRequest(reason);

                if (console.Minion != null)
                {
                    console.SendMessage(new DisplayRequestMessage(reason));

                    var request = new SessionMinionDisplayRequest(console, reason);

                    Logger.LogTrace("Created {request}", request);

                    return request;
                }
            }

            return await manager.CreateRequest(type, reason);
        }

        IAsyncEnumerator<IPowerRequest> IAsyncEnumerable<IPowerRequest>.GetAsyncEnumerator(CancellationToken token) => manager.GetAsyncEnumerator(token);

        /// <summary>
        /// A power request that intentionally does nothing — returned for display requests while
        /// no console session exists, satisfying the interface contract without holding anything.
        /// </summary>
        private sealed class IgnoredPowerRequest(string reason) : IPowerRequest
        {
            public string   Name    => "Ignored";
            public string?  Reason  => reason;

            public void Dispose() { }

            public override string ToString()
            {
                return $"IgnoredPowerRequest(why='{reason}')";
            }
        }
    }
}
