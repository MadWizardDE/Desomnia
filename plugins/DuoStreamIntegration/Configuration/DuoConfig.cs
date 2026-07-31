using MadWizard.Desomnia.Network.Configuration;

namespace MadWizard.Desomnia.Service.Duo.Configuration
{
    public class DuoConfig : Network.Configuration.ModuleConfig<NetworkMonitorConfig>
    {
        public DuoStreamMonitorConfig? DuoStreamMonitor { get; set; }

        internal bool UseFallback => (DuoStreamMonitor?.UseFallback ?? false) || NetworkMonitor.Count == 0;
    }
}
