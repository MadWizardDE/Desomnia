using MadWizard.Desomnia.Network.Configuration.Interfaces;

namespace MadWizard.Desomnia.Network.Configuration
{
    public class ModuleConfig<T> where T : NetworkMonitorConfig
    {
        public IList<T> NetworkMonitor { get; private set; } = [];

        /// <summary>
        /// Root-level (environment-scoped) interface blocks. An IList of a complex type, so
        /// the collection-element derivation marks it — the environment merge then APPENDS
        /// nameless blocks instead of fusing them.
        /// </summary>
        public IList<NetworkInterfaceBlockInfo> NetworkInterfaceBlock { get; private set; } = [];
    }
}
