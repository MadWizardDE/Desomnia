using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Handoff
{
    public class HandoffService : INetworkService
    {
        public required ILogger<HandoffService> Logger { private get; init; }

        public required SystemMonitor   System  { private get; init; }
        public required NetworkMonitor  Monitor { private get; init; }

        void INetworkService.Startup() => System.Suspend += System_Suspend;

        private async Task System_Suspend(Event data)
        {
            await HandoffLocalWatches(); // throw an exception if a handoff failed, but was required -> suspend cancelled
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
                using var scope = Logger.BeginHostScope(watch.Host);

                try
                {
                    await watch.HandoffWatch();
                }
                catch (Exception ex)
                {
                    if (!watch.HandoffOptions.IsRequired)
                    {
                        Logger.LogError(ex, "Could not handoff watch for '{Host}'.", watch.Host.Name);
                    }
                    else throw;
                }
            }
        }

        async void INetworkService.Resume()
        {
            foreach (var watch in Monitor.OfType<LocalHostWatch>())
            {
                using var scope = Logger.BeginHostScope(watch.Host);

                await watch.ReclaimWatch();
            }
        }

        void INetworkService.Shutdown() => System.Suspend -= System_Suspend;
    }
}
