using MadWizard.Desomnia.Network.Neighborhood;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Discovery
{
    public interface IIPAddressDiscovery
    {
        Task DiscoverIPAddresses(NetworkHost host, AddressFamily family);
    }
}
