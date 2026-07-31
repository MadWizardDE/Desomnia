using MadWizard.Desomnia.Events;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Session.Configuration;
using MadWizard.Desomnia.Session.Manager;


namespace MadWizard.Desomnia.Session
{
    public class SessionWatch : ResourceMonitor<SessionProcessWatch>
    {
        [EventContext]
        public required ISession Session { get; init; }

        public required Func<SessionProcessWatchInfo, SessionProcessWatch> CreateProcessWatch { private get; init; }

        public TimeSpan? MaxIdleTime { get; private set; }

        private ClockOptions Clock { get; set; } = new() { Time = true };

        public event EventInvocation? Login;
        public event EventInvocation? RemoteLogin;
        public event EventInvocation? ConsoleLogin;
        public event EventInvocation? RemoteConnect;
        public event EventInvocation? ConsoleConnect;

        // NEW behavior (release-noted, spec §9.3): a delayed onDisconnect is aborted by a
        // reconnect and vice versa; lock/unlock likewise — expected by analogy with
        // Idle/Demand, never implemented before
        [EventOpposite(nameof(ConsoleConnect), nameof(RemoteConnect))]
        public event EventInvocation? Disconnect;

        public event EventInvocation? Unlock;

        [EventOpposite(nameof(Unlock))]
        public event EventInvocation? Lock;

        public event EventInvocation? Logout;

        public SessionWatch(ISession session)
        {
            session.Connected += Session_Connected;
            session.Disconnected += Session_Disconnected;
            session.Unlocked += Session_Unlocked;
            session.Locked += Session_Locked;
        }

        private void Session_Connected(object? sender, EventArgs e)
        {
            if (Session.IsConsoleConnected)
                ConsoleConnect.TriggerEvent();
            else if (Session.IsRemoteConnected)
                RemoteConnect.TriggerEvent();
        }

        private void Session_Disconnected(object? sender, EventArgs e) => Disconnect.TriggerEvent();
        private void Session_Unlocked(object? sender, EventArgs e) => Unlock.TriggerEvent();
        private void Session_Locked(object? sender, EventArgs e) => Lock.TriggerEvent();

        internal void ApplyConfiguration(SessionMonitorConfig config, SessionWatchDescriptor desc)
        {
            if (MaxIdleTime == null || MaxIdleTime.Value < desc.MaxIdleTime)
                MaxIdleTime = desc.MaxIdleTime;

            Clock += desc.MakeClockOptions(config);

            GetEvent(nameof(Idle)).AddAction(desc.OnIdle);
            Login.AddAction(desc.OnLogin);
            RemoteLogin.AddAction(desc.OnRemoteLogin);
            ConsoleLogin.AddAction(desc.OnConsoleLogin);
            RemoteConnect.AddAction(desc.OnRemoteConnect);
            ConsoleConnect.AddAction(desc.OnConsoleConnect);
            Disconnect.AddAction(desc.OnDisconnect);
            Unlock.AddAction(desc.OnUnlock);
            Lock.AddAction(desc.OnLock);
            Logout.AddAction(desc.OnLogout);

            foreach (var info in desc.Process)
            {
                this.StartTracking(CreateProcessWatch(info));
            }
        }

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            var token = new SessionUsage(Session);

            foreach (var processToken in base.InspectResource(interval))
                token.Tokens.Add(processToken);

            if (HadUsageSince(interval) || token.Tokens.Count > 0)
                yield return token;
        }

        private bool HadUsageSince(TimeSpan interval)
        {
            if (Clock.Time)
            {
                if (Session.IsRemoteConnected && !Clock.Remote)
                {
                    return true;
                }
                else if ((Clock.Disconnected || Session.IsConnected) && Session.IdleTime is TimeSpan time)
                {
                    if (time < (MaxIdleTime ?? interval))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [ActionHandler("lock")]
        internal void HandleActionLock() => Session.Lock();
        [ActionHandler("logout")]
        internal void HandleActionLogout() => Session.Logoff();
        [ActionHandler("disconnect")]
        internal void HandleActionDisconnect() => Session.Disconnect();

        internal void TriggerLogon()
        {
            Login.TriggerEvent();

            if (Session.IsRemoteConnected)
            {
                RemoteLogin.TriggerEvent();
            }
            else if (Session.IsConsoleConnected)
            {
                ConsoleLogin.TriggerEvent();
            }
        }

        internal void TriggerLogout()
        {
            Logout.TriggerEvent(); 
        }

        public override void Dispose()
        {
            Session.Locked -= Session_Locked;
            Session.Unlocked -= Session_Unlocked;
            Session.Disconnected -= Session_Disconnected;
            Session.Connected -= Session_Connected;

            base.Dispose();
        }
    }
}
