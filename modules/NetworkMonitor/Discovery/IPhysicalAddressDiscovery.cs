using MadWizard.Desomnia.Network.Neighborhood;

namespace MadWizard.Desomnia.Network.Discovery
{
    public interface IPhysicalAddressDiscovery
    {
        Task DiscoverAddress(NetworkHost host);
    }
}
