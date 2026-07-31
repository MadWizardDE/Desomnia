using MadWizard.Desomnia.Processes;
using MadWizard.Desomnia.Session.Configuration;
using MadWizard.Desomnia.Session.Manager;
using MadWizard.Desomnia.Events;

namespace MadWizard.Desomnia.Session
{
    public class SessionProcessWatch : ProcessWatch
    {
        [EventContext]
        public required ISession Session
        {
            get;
            init
            {
                field = value;

                field.Connected += Session_Connected;
                field.Disconnected += Session_Disconnected;
            }
        }

        [EventOpposite(nameof(SessionDemand))]
        public event EventInvocation? SessionIdle;
        public event EventInvocation? SessionDemand;

        public event EventInvocation? SessionConsoleConnected;
        public event EventInvocation? SessionRemoteConnected;

        [EventOpposite(nameof(SessionConsoleConnected), nameof(SessionRemoteConnected))]
        public event EventInvocation? SessionDisconnected;

        public SessionProcessWatch(SessionProcessWatchInfo info) : base(info)
        {
            SessionIdle.AddAction(info.OnSessionIdle);
            SessionDemand.AddAction(info.OnSessionDemand);
            SessionConsoleConnected.AddAction(info.OnSessionConsoleConnect);
            SessionRemoteConnected.AddAction(info.OnSessionRemoteConnect);
            SessionDisconnected.AddAction(info.OnSessionDisconnect);
        }

        #region SessionWatch events
        protected override void OnAttachedTo(EventMetaObject parent)
        {
            if (parent is ResourceMonitor monitor)
            {
                monitor.Idle += SessionWatch_Idle;
                monitor.Demand += SessionWatch_Demand;
            }
        }

        protected override void OnDetachedFrom(EventMetaObject parent)
        {
            if (parent is ResourceMonitor monitor)
            {
                monitor.Demand -= SessionWatch_Demand;
                monitor.Idle -= SessionWatch_Idle;
            }
        }

        private async Task SessionWatch_Idle(Event data)
        {
            await SessionIdle.TriggerEventAsync();                 // cancels SessionDemand's pending (annotation)
        }

        private async Task SessionWatch_Demand(Event data)
        {
            await SessionDemand.TriggerEventAsync();
        }
        #endregion

        #region Session events
        private void Session_Connected(object? sender, EventArgs e)
        {
            // cancellation of SessionDisconnected's pending happens per triggered event
            // (annotation) — the old IsConnected pre-gate was aesthetic and is gone (§9.3)
            if (Session.IsConsoleConnected)
                SessionConsoleConnected.TriggerEvent();
            if (Session.IsRemoteConnected)
                SessionRemoteConnected.TriggerEvent();

            // defensive: a transitional WTS state can report Connected before the
            // protocol is classified — neither event fires then, but the pending
            // disconnect action must still be aborted (matches the old behavior)
            if (!Session.IsConsoleConnected && !Session.IsRemoteConnected)
                SessionDisconnected.Cancel();
        }

        private void Session_Disconnected(object? sender, EventArgs e)
        {
            SessionDisconnected.TriggerEvent();
        }
        #endregion

        public override void Dispose()
        {
            Session.Disconnected -= Session_Disconnected;
            Session.Connected -= Session_Connected;

            base.Dispose();
        }
    }
}
