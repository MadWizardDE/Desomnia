using MadWizard.Desomnia.Network.Address;
using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Handoff
{
    public class HandoffService : INetworkService
    {
        public required ILogger<HandoffService> Logger { private get; init; }

        public required SystemMonitor   System  { private get; init; }
        public required NetworkMonitor  Monitor { private get; init; }

        public required AddressMappingService AddressMapping { private get; init; }

        void INetworkService.Startup() => System.Suspend += System_Suspend;

        private async Task System_Suspend(Event data)
        {
            await HandoffLocalWatches(); // will throw exception if a handoff failed, but was required
        }

        async void INetworkService.Suspend()
        {
            try
            {
                await HandoffLocalWatches();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not handoff local watches");
            }
        }

        private async Task HandoffLocalWatches()
        {
            foreach (var watch in Monitor.OfType<LocalHostWatch>())
            {
                await watch.MaybeHandoffWatch();
            }
        }

        async void INetworkService.Resume()
        {
            await MaybeAdvertiseWatch();
        }

        private async Task MaybeAdvertiseWatch()
        {
            foreach (var watch in Monitor.OfType<HostDemandWatch>())
            {
                using var scope = Logger.BeginHostScope(watch.Host);

                if (watch.Host.IPAddresses.Where(watch.AdvertiseOptions.ShouldAdvertiseOnLocalHostResume) is var ips && ips.Any())
                {
                    Logger.LogDebug($"Resuming operation, taking ownership of watched IP addresses...");

                    foreach (var ip in ips)
                    {
                        if (await watch.RequestIPUnicastTrafficTo(ip) is PhysicalAddress mac)
                        {
                            AddressMapping.Advertise(new(ip, mac));
                        }
                    }
                }
            }
        }

        void INetworkService.Shutdown() => System.Suspend -= System_Suspend;
    }
}
