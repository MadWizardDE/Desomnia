using MadWizard.Desomnia.Network.Filter;
using MadWizard.Desomnia.Network.Neighborhood;

namespace MadWizard.Desomnia.Network.Watch
{
    public class ServiceFilterWatch(NetworkService service) : NetworkServiceWatch(service)
    {
        public required Lazy<IPacketFilter> Filter { internal get; init; }
    }
}
