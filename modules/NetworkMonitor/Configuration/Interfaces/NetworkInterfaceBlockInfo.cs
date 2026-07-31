namespace MadWizard.Desomnia.Network.Configuration.Interfaces
{
    /// <summary>
    /// One &lt;NetworkInterfaceBlock&gt; element: an interface (or several, the pattern
    /// notation of the "interface" attribute) to keep out of service. Placed inside a
    /// NetworkMonitor the block lives with that monitor — prioritizing its interface over
    /// another, e.g. take the internal WiFi out of service as long as the docked ethernet
    /// is up; placed at the SystemMonitor root (typically merged in by an environment) it
    /// stands on its own.
    /// </summary>
    public class NetworkInterfaceBlockInfo
    {
        /// <summary>Which interfaces to block — same notation as the NetworkMonitor
        /// "interface" attribute (a regex against the id, the display name on Windows).</summary>
        public required string Interface { get; set; }

        /// <summary>Whether to enforce the block against foreign re-enables; by default a
        /// user flipping the interface back on wins.</summary>
        public bool Force { get; set; }
    }
}
