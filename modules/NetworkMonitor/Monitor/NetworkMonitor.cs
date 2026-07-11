using Autofac.Features.Metadata;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Watch;
using MadWizard.Desomnia.Power.Guard;
using Microsoft.Extensions.Logging;
using PacketDotNet;

namespace MadWizard.Desomnia.Network
{
    public class NetworkMonitor : ResourceMonitor<NetworkHostWatch>, IPowerTransitionGuard
    {
        public required ILogger<NetworkMonitor> Logger { private get; init; }

        public required string Name { get; init; }

        public required WatchOptions Options { get; init; }

        public required NetworkDevice   Device  { private get; init; }
        public required NetworkSegment  Network { private get; init; }
        public required NetworkJanitor  Janitor { private get; init; }

#if DESOMNIA_AOT
        // NativeAOT: strongly-typed Meta<T, TMetadata> builds its view via MakeGenericMethod over the
        // metadata property types (int Order), which NativeAOT cannot JIT. Fall back to loosely-typed
        // Meta<T> and read the metadata dictionary by key (default 0, matching [DefaultValue(0)]).
        public IEnumerable<Meta<INetworkService>> Services { private get; init; } = [];

        private IEnumerable<INetworkService> OrderedServices => Services
            .OrderBy(s => s.Metadata.TryGetValue(nameof(INetworkService.Metadata.Order), out var order) && order is int o ? o : 0)
            .Select(s => s.Value);
#else
        public IEnumerable<Meta<INetworkService, INetworkService.Metadata>> Services { private get; init; } = [];

        private IEnumerable<INetworkService> OrderedServices => Services.OrderBy(s => s.Metadata.Order).Select(s => s.Value);
#endif

        public event EventInvocation? Connected;
        public event EventInvocation? Disconnected;

        public NetworkHostWatch? this[NetworkHost host] => this.FirstOrDefault(w => w.Host == host);

        public bool IsWatchedBy<T>(EthernetPacket packet) where T : NetworkHostWatch
        {
            return this.OfType<T>().Any(w => w.Host.HasAddress(packet.FindTargetPhysicalAddress(), packet.FindTargetIPAddress()));
        }

        public override IEnumerable<UsageToken> Inspect(TimeSpan interval)
        {
            using (Network.Mutex.Lock())
            {
                return base.Inspect(interval);
            }
        }

        async Task IPowerTransitionGuard.BeforeTransition(PowerTransition transition)
        {
            if (transition == PowerTransition.Suspend)
            {
                foreach (var service in OrderedServices)
                {
                    await service.BeforeSuspend();
                }
            }
        }

        internal async Task StartMonitoring()
        {
            Device.StartCapture();
            Device.EthernetCaptured += HandlePacket;

            foreach (var service in OrderedServices)
                await service.Startup();

            TriggerEvent(nameof(Connected));

            Janitor.StartSweeping();

            Logger.LogDebug($"Monitoring of '{Name}' has been started.");
        }

        internal async Task StartWatch()
        {
            foreach (var watch in this)
                await watch.StartWatch();
        }

        internal void ResumeMonitoring()
        {
            Logger.LogDebug($"Monitoring of '{Name}' will now continue...");

            Device.StartCapture();

            foreach (var service in OrderedServices)
                service.Resume();
        }

        private void HandlePacket(object? sender, EthernetPacket packet)
        {
            using (Network.Mutex.Lock())
            {
                foreach (var service in OrderedServices)
                {
                    service.ProcessPacket(packet);
                }
            }
        }

        internal void SuspendMonitoring()
        {
            foreach (var service in OrderedServices.Reverse())
                service.Suspend();

            Device.StopCapture();

            Logger.LogDebug($"Monitoring of '{Name}' has been paused.");
        }

        internal async Task StopMonitoring(NetworkShutdownReason reason)
        {
            Janitor.StopSweeping();

            foreach (var watch in this)
                await watch.StopWatch(reason == NetworkShutdownReason.ApplicationShutdown);

            foreach (var service in OrderedServices.Reverse())
                await service.Shutdown(reason);

            Device.EthernetCaptured -= HandlePacket;
            Device.StopCapture();

            TriggerEvent(nameof(Disconnected));

            Logger.LogDebug($"Monitoring of '{Name}' has been stopped.");
        }
    }
}