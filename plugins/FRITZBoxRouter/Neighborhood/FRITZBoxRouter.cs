using MadWizard.Desomnia.Network.FRITZ.API;
using MadWizard.Desomnia.Network.FRITZ.API.Model;
using MadWizard.Desomnia.Network.Neighborhood;
using System.Net;

namespace MadWizard.Desomnia.Network.FRITZ.Neighborhood
{
    /// <summary>
    /// A configured FRITZ!Box at runtime — a <see cref="NetworkRouter"/> that also speaks the
    /// box' APIs. It lives in the network's normal host container (<see cref="NetworkSegment"/>),
    /// so consumers find it by enumerating the segment (<c>network.OfType&lt;FRITZBox&gt;()</c>)
    /// or by name via the segment indexer; there is no separate registry.
    ///
    /// <para>Ports are deliberately <em>not</em> config citizens — they are resolved live against
    /// the box whenever an action needs one, so the configuration only ever names the box, never
    /// enumerates its (mutable) hardware. The <see cref="FRITZBoxClient"/> is owned by the router's
    /// host scope and disposed with it.</para>
    /// </summary>
    public class FRITZBoxRouter(string name, FRITZBoxClient client) : NetworkRouter(name)
    {
        /// <summary>How long a fetched host table is reused before the box is queried again — short
        /// enough to stay current, long enough that a discovery pass touching many hosts hits the box once.</summary>
        private static readonly TimeSpan HostCacheTTL = TimeSpan.FromSeconds(15);

        private readonly SemaphoreSlim _hostLock = new(1, 1);
        private IReadOnlyList<FritzHost>? _hostCache;
        private DateTime _hostCachedAt;

        /// <summary>Every host the box knows (leased devices with their MAC, plus VPN peers). The
        /// result is cached per box for <see cref="HostCacheTTL"/>, so repeated lookups during a
        /// discovery pass share one enumeration — and each box keeps its own view (routers in a mesh
        /// don't report identical tables).</summary>
        public async Task<IReadOnlyList<FritzHost>> GetHostsAsync(CancellationToken ct = default)
        {
            if (_hostCache is { } fresh && DateTime.Now - _hostCachedAt < HostCacheTTL)
                return fresh;

            await _hostLock.WaitAsync(ct);
            try
            {
                if (_hostCache is { } stillFresh && DateTime.Now - _hostCachedAt < HostCacheTTL)
                    return stillFresh;

                var hosts = await client.GetHostsAsync(ct);

                _hostCachedAt = DateTime.Now;
                return _hostCache = hosts;
            }
            finally
            {
                _hostLock.Release();
            }
        }

        /// <summary>The box' VPN peers — layer-3 hosts with a fixed tunnel IP and no MAC.</summary>
        public async Task<IReadOnlyList<FritzHost>> GetVpnClientsAsync(CancellationToken ct = default)
            => [.. (await GetHostsAsync(ct)).Where(h => h.IsVPN)];

        /// <summary>The public IPv4 this box presents on its WAN uplink, queried live from the box
        /// (unauthenticated). <c>null</c> when the box has no WAN of its own (a mesh slave) or its
        /// uplink is currently down.</summary>
        public Task<IPAddress?> GetExternalIPv4Async(CancellationToken ct = default)
            => client.GetExternalIPv4Async(ct);

        /// <summary>Resolves a port by <c>ifname</c> (e.g. <c>eth0</c>), <c>label</c>
        /// (e.g. <c>LAN:1</c>) or <c>UID</c>, live from the box. Returns null if none matches.</summary>
        public async Task<EthernetPort?> ResolvePortAsync(string idOrLabel, CancellationToken ct = default)
        {
            var ports = await client.GetEthPortsAsync(ct);

            return ports.Eth.FirstOrDefault(p =>
                   Eq(p.IfName, idOrLabel)
                || Eq(p.Label, idOrLabel)
                || Eq(p.Uid, idOrLabel));

            static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sets a port's configured speed cap. <paramref name="eeeMode"/> defaults to the
        /// port's current value (the web UI always sends it back alongside the change).</summary>
        public async Task SetPortMaxSpeedAsync(EthernetPort port, int maxSpeed, string? eeeMode = null, CancellationToken ct = default)
        {
            if (!port.AllowedSpeeds.Contains(maxSpeed))
                throw new FRITZBoxAPIException(
                    $"maxspeed {maxSpeed} is not allowed for {port.Label} — pick one of {port.SpeedList}.");

            await client.PutEthPortAsync(port.Uid, new EthernetPortUpdate
            {
                MaxSpeed = maxSpeed,
                EeeMode = eeeMode ?? port.EeeMode,
            }, ct);
        }
    }
}
