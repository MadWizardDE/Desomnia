using MadWizard.Desomnia.Network.Neighborhood.Services;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Context
{
    internal static class NetworkContextLookup
    {
        extension (NetworkContext context)
        {
            internal NetworkHostContext? FindHostContextBy(PhysicalAddress? physical) => context.FirstOrDefault(ctx => physical?.Equals(ctx.Host.PhysicalAddress) ?? false);
        }

        extension (NetworkHostContext context)
        {
            internal NetworkHostServiceContext? FindServiceContextBy(IPPort port) => context.FirstOrDefault(ctx => ctx.Service is TransportNetworkService t && t.Port == port);
        }
    }
}
