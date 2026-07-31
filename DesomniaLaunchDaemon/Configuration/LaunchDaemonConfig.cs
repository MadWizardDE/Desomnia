using MadWizard.Desomnia.Processes.Configuration;

namespace MadWizard.Desomnia.LaunchDaemon.Configuration
{
    public class LaunchDaemonConfig
    {
        /// <summary>
        /// The same &lt;ProcessMonitor&gt; element the module binds, read here for its
        /// <c>pollInterval</c> alone — the platform manager has to be built with it.
        /// </summary>
        public ProcessManagerConfig? ProcessMonitor { get; set; }
    }
}
