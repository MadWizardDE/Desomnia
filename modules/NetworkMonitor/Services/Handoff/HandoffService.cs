using MadWizard.Desomnia.Network.Watch;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.Handoff
{
    public class HandoffService(NetworkMonitor monitor) : INetworkService
    {
        public required ILogger<HandoffService> Logger { private get; init; }

        void INetworkService.Startup() { }

        async Task INetworkService.BeforeSuspend()
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
            var watches = monitor.OfType<LocalHostWatch>().Where(watch => watch.HandoffNeeded);

            if (watches.Any())
            {
                Logger.LogDebug("Handing off local watches...");

                foreach (var watch in watches)
                {
                    using var scope = Logger.BeginHostScope(watch.Host);

                    Logger.LogDebug("Handing off local watch for '{Host}'...", watch.Host.Name);

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
        }

        async void INetworkService.Resume()
        {
            foreach (var watch in monitor.OfType<LocalHostWatch>())
            {
                using var scope = Logger.BeginHostScope(watch.Host);

                await watch.ReclaimWatch();
            }
        }

        void INetworkService.Shutdown() { }
    }
}
