using System.Net;

namespace MadWizard.Desomnia.Network.Discovery
{
    /// <summary>
    /// Resolves the network's public-facing IPv4 address — the address the segment presents to the
    /// internet. A resolution <em>strategy</em>, deliberately not tied to any one host: one
    /// implementation asks the segment's routers for their WAN address, another may query an
    /// internet echo service that needs no cooperation from the neighborhood at all. Yields the
    /// address, or <c>null</c> when it cannot be determined.
    /// </summary>
    public interface IExternalIPAddressDiscovery
    {
        /// <summary>Resolves the current external IPv4 address, or <c>null</c> if it cannot be determined.</summary>
        Task<IPAddress?> ResolveExternalAddress(CancellationToken ct = default);
    }
}
