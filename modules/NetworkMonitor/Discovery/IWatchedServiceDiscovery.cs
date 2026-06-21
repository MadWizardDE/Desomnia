using MadWizard.Desomnia.Network.Watch;

namespace MadWizard.Desomnia.Network.Discovery
{
    public interface IWatchedServiceDiscovery
    {
        Task DiscoverServices(NetworkHostWatch watch);
    }

    internal class CompositeWatchedServiceDiscovery(IEnumerable<IWatchedServiceDiscovery> discoverers) : IWatchedServiceDiscovery
    {
        public async Task DiscoverServices(NetworkHostWatch watch)
        {
            foreach (var discoverer in discoverers)
            {
                await discoverer.DiscoverServices(watch);
            }
        }
    }

}
