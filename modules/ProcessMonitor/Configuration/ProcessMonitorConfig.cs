using MadWizard.Desomnia.Configuration;

namespace MadWizard.Desomnia.Processes.Configuration
{
    public class ProcessManagerConfig
    {
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(2);

        public TimeSpan? PollInterval { get; init; }
    }

    public class ProcessMonitorConfig : ProcessManagerConfig
    {
        public DelayedActionInfo? OnIdle { get; set; }
        public DelayedActionInfo? OnDemand { get; set; }

        public IList<ProcessWatchInfo> Process { get; set; } = [];
    }
}
