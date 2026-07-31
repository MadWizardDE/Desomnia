using MadWizard.Desomnia.Configuration;

namespace MadWizard.Desomnia.Service.Duo.Configuration
{
    public class DuoStreamMonitorConfig
    {
        public required string ServiceName              { get; set; } = "DuoService";

        public TimeSpan PollInterval                    { get; set; } = TimeSpan.FromSeconds(1);

        public bool UseFallback                         { get; set; } = false;
        public bool UsePolling                          { get; set; } = false;

        public DelayedActionInfo? OnIdle                { get; set; }
        public DelayedActionInfo? OnDemand              { get; set; }

        public DelayedActionInfo? OnInstanceDemand      { get; set; }

        public DelayedActionInfo? OnInstanceIdle        { get; set; }

        public DelayedActionInfo? OnInstanceLogin       { get; set; }
        public DelayedActionInfo? OnInstanceStarted     { get; set; }
        public DelayedActionInfo? OnInstanceStopped     { get; set; }
        public DelayedActionInfo? OnInstanceLogout      { get; set; }

        public IList<DuoInstanceInfo> Instance { get; private set; } = [];

        internal DuoInstanceInfo? this[string name] => Instance.FirstOrDefault(i => i.Name == name);
    }
}
