using MadWizard.Desomnia.Network.Neighborhood;

namespace MadWizard.Desomnia.Network.Discovery
{
    public interface IServiceDiscovery
    {
        Task DiscoverServices(NetworkSegment network);
    }
}
