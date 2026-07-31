using MadWizard.Desomnia.Configuration;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Session.Manager;

namespace MadWizard.Desomnia.Session.Configuration
{
    public abstract class SessionMonitorConfig<TConfig, TDesc>
        where TConfig : SessionMonitorConfig<TConfig, TDesc>
        where TDesc : SessionDescriptor
    {
        public DelayedActionInfo? OnIdle { get; set; }
        public DelayedActionInfo? OnDemand { get; set; }

        public delegate void ConfigureWithDescriptior(TConfig config, TDesc desc);

        public TDesc? Everyone { get; set; }
        public TDesc? Administrator { get; set; }

        public IList<TDesc> User { get; set; } = [];

        public void Configure<S>(S session, ConfigureWithDescriptior configure) where S : ISession
        {
            var self = (TConfig)this; // safe if inheritance is correct

            if (this.Everyone is TDesc desc)
                configure(self, desc);

            if (session.IsUser)
                foreach (var userDesc in this.User)
                    if (userDesc.Name?.Match(session.UserName) ?? true)
                        configure(self, userDesc);

            if (session.IsAdministrator)
                if (this.Administrator is TDesc adminDesc)
                    configure(self, adminDesc);
        }
    }

    public abstract class SessionDescriptor
    {
        public SessionMatcher? Name { get; set; }
    }

    public class SessionWatchDescriptor : SessionDescriptor
    {
        public TimeSpan? MaxIdleTime { get; set; }

        #region Session :: ClockOptions
        private bool? ClockTime { get; set; }
        private bool? ClockRemote { get; set; }
        private bool? ClockDisconnected { get; set; }

        public virtual ClockOptions MakeClockOptions(SessionMonitorConfig monitor) => new()
        {
            Time = this.ClockTime ?? monitor.ClockTime,
            Remote = this.ClockRemote ?? monitor.ClockRemote,
            Disconnected = this.ClockDisconnected ?? monitor.ClockDisconnected,
        };
        #endregion

        public ScheduledActionInfo? OnIdle { get; set; }
        public ScheduledActionInfo? OnLogin { get; set; }
        public ScheduledActionInfo? OnRemoteLogin { get; set; }
        public ScheduledActionInfo? OnConsoleLogin { get; set; }
        public ScheduledActionInfo? OnRemoteConnect { get; set; }
        public ScheduledActionInfo? OnConsoleConnect { get; set; }
        public ScheduledActionInfo? OnDisconnect { get; set; }
        public ScheduledActionInfo? OnUnlock { get; set; }
        public ScheduledActionInfo? OnLock { get; set; }
        public ScheduledActionInfo? OnLogout { get; set; }

        public IList<SessionProcessWatchInfo> Process { get; set; } = [];
    }

    public class SessionMonitorConfig : SessionMonitorConfig<SessionMonitorConfig, SessionWatchDescriptor>
    {
        internal bool RegisterWithSleepProxy { get; set; } = true;

        #region SessionMonitor :: ClockOptions
        internal bool ClockTime { get; set; } = true;
        internal bool ClockRemote { get; set; } = false;
        internal bool ClockDisconnected { get; set; } = false;
        #endregion
    }
}