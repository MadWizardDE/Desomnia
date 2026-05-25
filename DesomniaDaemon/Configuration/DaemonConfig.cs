using MadWizard.Desomnia.PowerRequest.Configuration;

namespace MadWizard.Desomnia.Daemon.Configuration
{
    public class DaemonConfig
    {
        public bool UseDBus { get; set; } = true;

        public PowerManagerConfig PowerRequestMonitor { get; set; } = new PowerManagerConfig();
    }
}
