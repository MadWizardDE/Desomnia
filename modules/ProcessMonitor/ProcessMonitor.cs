using Autofac;
using MadWizard.Desomnia.Processes.Configuration;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Processes
{
    public class ProcessMonitor(ProcessMonitorConfig config) : ResourceMonitor<ProcessWatch>, IStartable
    {
        public required ILogger<ProcessMonitor> Logger { get; set; }

        public required Func<ProcessWatchInfo, ProcessWatch> CreateWatch { private get; init; }

        void IStartable.Start()
        {
            GetEvent(nameof(Idle)).AddAction(config.OnIdle);
            GetEvent(nameof(Demand)).AddAction(config.OnDemand);

            foreach (var info in config.Process)
            {
                StartTracking(CreateWatch(info));
            }

            Logger.LogDebug("Startup complete; {Count} procceses watched.", config.Process.Count);
        }
    }
}