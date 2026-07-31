using MadWizard.Desomnia.Network.Watch;

namespace MadWizard.Desomnia.Network.Discovery
{
    public interface IWatchedServiceDiscovery
    {
        Task DiscoverServices(NetworkHostWatch watch);
    }
}
