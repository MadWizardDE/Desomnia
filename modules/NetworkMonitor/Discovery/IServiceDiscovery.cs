using MadWizard.Desomnia.Network.Neighborhood;

namespace MadWizard.Desomnia.Network.Discovery
{
    public interface IServiceDiscovery
    {
        Task DiscoverServices(NetworkSegment network);
    }

    internal class CompositeServiceDiscovery(IEnumerable<IServiceDiscovery> discoverers) : IServiceDiscovery
    {
        public async Task DiscoverServices(NetworkSegment network)
        {
            foreach (var discoverer in discoverers)
            {
                await discoverer.DiscoverServices(network);
            }
        }
    }
}
