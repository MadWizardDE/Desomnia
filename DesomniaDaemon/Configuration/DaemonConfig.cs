using MadWizard.Desomnia.PowerRequest.Configuration;
using MadWizard.Desomnia.Processes.Configuration;

namespace MadWizard.Desomnia.Daemon.Configuration
{
    public class DaemonConfig
    {
        public bool UseDBus { get; set; } = true;

        /// <summary>
        /// The same &lt;ProcessMonitor&gt; element the module binds, read here for its
        /// <c>pollInterval</c> alone — the platform manager has to be built with it.
        /// </summary>
        public ProcessManagerConfig? ProcessMonitor { get; set; }

        public PowerManagerConfig PowerRequestMonitor { get; set; } = new PowerManagerConfig();
    }
}
