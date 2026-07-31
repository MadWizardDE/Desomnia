using MadWizard.Desomnia.Network.Discovery;
using MadWizard.Desomnia.Network.FRITZ.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood;
using Microsoft.Extensions.Logging;
using System.Net;

namespace MadWizard.Desomnia.Network.FRITZ.Discovery
{
    /// <summary>
    /// Resolves the network's external IPv4 by asking the FRITZ!Box routers on the segment for their
    /// WAN address (an unauthenticated IGD query): the mesh master answers with the public IP, a
    /// mesh slave with none. The first box that reports an address wins.
    ///
    /// <para>This is the neighborhood-based <see cref="IExternalIPAddressDiscovery"/> — it depends on
    /// a cooperating router being present on the segment. Other implementations may resolve the
    /// address independently of the neighborhood (e.g. via an internet echo service).</para>
    /// </summary>
    internal sealed class FRITZBoxExternalAddressDetector : IExternalIPAddressDiscovery
    {
        public required ILogger<FRITZBoxExternalAddressDetector> Logger { private get; init; }

        public required NetworkSegment Network { private get; init; }

        public async Task<IPAddress?> ResolveExternalAddress(CancellationToken ct = default)
        {
            foreach (var box in Network.OfType<FRITZBoxRouter>())
            {
                try
                {
                    if (await box.GetExternalIPv4Async(ct) is IPAddress ip)
                    {
                        return ip;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.LogDebug(ex, "FRITZ!Box '{Name}' external address query failed.", box.Name);
                }
            }

            return null;
        }
    }
}
