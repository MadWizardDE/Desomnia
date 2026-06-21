using MadWizard.Desomnia.Network.Neighborhood;

namespace MadWizard.Desomnia.Network.Discovery.BuiltIn
{
    internal class SleepProxyDetector : IServiceDiscovery
    {
        Task IServiceDiscovery.DiscoverServices(NetworkSegment network)
        {
            throw new NotImplementedException();
        }
    }
}
