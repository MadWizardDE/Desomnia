using Autofac;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Context.Bridges
{
    /// <summary>
    /// Explicit inspection hand-off (EVENT-SYSTEM-REDESIGN.md §7.2): the scope owner is
    /// the only party that knows when a NetworkMonitor is ready, so it — not a
    /// sync-on-inspect resolve dance — decides when the monitor joins the inspection
    /// roster and gains its tree parent. A separate bridge entity, so the observer
    /// never references the SystemMonitor. Exception-safe: a tracking failure must
    /// never tear down a freshly started network context.
    /// </summary>
    public class SystemMonitorBridge(DynamicNetworkObserver observer, SystemMonitor monitor) : IStartable
    {
        public required ILogger<SystemMonitorBridge> Logger { private get; init; }

        void IStartable.Start()
        {
            observer.MonitoringStarted += (_, network) => Handle(network, track: true);
            observer.MonitoringStopped += (_, network) => Handle(network, track: false);
        }

        private void Handle(NetworkMonitor network, bool track)
        {
            try
            {
                if (track)
                    monitor.StartTracking(network);
                else
                    monitor.StopTracking(network);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to {(track ? "start" : "stop")} inspecting '{network.Name}'");
            }
        }
    }
}
