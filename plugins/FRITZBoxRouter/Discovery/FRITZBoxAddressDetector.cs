using MadWizard.Desomnia.Network.Discovery;
using MadWizard.Desomnia.Network.FRITZ.API.Model;
using MadWizard.Desomnia.Network.FRITZ.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.FRITZ.Discovery
{
    /// <summary>
    /// Fills in a tracked host's MAC (and IP) from the FRITZ!Box' known-host table. Joins the
    /// network's address-discovery composites, so it runs alongside the built-in ARP/NDP/DNS
    /// detectors and only supplies what they could not: crucially, the box remembers a host's
    /// MAC from its DHCP lease even while the host is asleep and absent from the ARP cache — which
    /// is exactly when Desomnia needs the MAC to wake it.
    ///
    /// <para>The boxes are found where every router lives: in the <see cref="NetworkSegment"/>,
    /// by their type — so this works for however many &lt;FRITZBoxRouter&gt; elements the network
    /// declares, without any side registry.</para>
    ///
    /// <para>Best-effort and purely additive: it never overwrites an address that is already
    /// known, and any box error is logged and swallowed so a host's discovery still completes.</para>
    /// </summary>
    internal sealed class FRITZBoxAddressDetector : IPhysicalAddressDiscovery, IIPAddressDiscovery
    {
        public required ILogger<FRITZBoxAddressDetector> Logger { private get; init; }

        public required NetworkSegment Network { private get; init; }

        async Task IPhysicalAddressDiscovery.DiscoverAddress(NetworkHost host)
        {
            if (host.PhysicalAddress is not null)
                return;

            var hosts = await GetHostsAsync();

            var entry = Match(hosts, host).FirstOrDefault(h => h.MAC is not null);

            if (entry?.MAC is PhysicalAddress mac)
            {
                host.PhysicalAddress = mac;

                Logger.LogHostPhysicalAddressChanged(host, mac);
            }
        }

        async Task IIPAddressDiscovery.DiscoverIPAddresses(NetworkHost host, AddressFamily family)
        {
            foreach (var entry in Match(await GetHostsAsync(), host))
            {
                if (entry.IP is { } ip && ip.AddressFamily == family && host.AddAddress(ip, new IPAddressOptions(IPAddressFlags.None)))
                {
                    Logger.LogHostAddressAdded(host, ip);
                }
            }
        }

        /// <summary>Lease entries that plausibly belong to <paramref name="host"/> — matched by a
        /// shared IP first, else by (host/friendly) name.</summary>
        private static IEnumerable<FritzHost> Match(IReadOnlyList<FritzHost> hosts, NetworkHost host)
        {
            var byIp = hosts.Where(h => h.IP is not null && host.HasAddress(ip: h.IP)).ToList();
            if (byIp.Count > 0)
                return byIp;

            return hosts.Where(h =>
                   string.Equals(h.Name, host.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(h.Name, host.HostName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The merged host tables of every FRITZ!Box on the segment. Each box caches its own
        /// table (see <see cref="FRITZBoxRouter.GetHostsAsync"/>), so this always reflects the boxes
        /// currently present — a box that joins the segment later is picked up on the next call — while
        /// still hitting each box at most once per its cache window.</summary>
        private async Task<IReadOnlyList<FritzHost>> GetHostsAsync()
        {
            var merged = new List<FritzHost>();

            foreach (var box in Network.OfType<FRITZBoxRouter>())
            {
                try
                {
                    merged.AddRange(await box.GetHostsAsync());
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "FRITZ!Box '{Name}' host enumeration failed.", box.Name);
                }
            }

            return merged;
        }
    }
}
