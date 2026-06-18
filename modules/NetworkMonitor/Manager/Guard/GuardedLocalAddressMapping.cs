using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Manager.Guard
{
    /// <summary>
    /// Decorates an <see cref="ILocalAddressMapping"/> and keeps a ledger of every mapping it
    /// installed:
    /// <list type="bullet">
    /// <item>An <c>Update</c> records the mapping before forwarding it to the platform.</item>
    /// <item>A <c>Delete</c> is only forwarded if the mapping is actually present – some platforms
    /// raise errors when asked to remove a neighbour-cache entry that does not exist.</item>
    /// <item>On disposal (i.e. when the network scope is torn down) any mapping that was never
    /// cleaned up is purged (with a warning), so the application never leaves stale entries behind.</item>
    /// </list>
    /// </summary>
    internal class GuardedLocalAddressMapping(ILocalAddressMapping local) : ILocalAddressMapping, IDisposable
    {
        public required ILogger<GuardedLocalAddressMapping> Logger { private get; init; }

        readonly ConcurrentDictionary<IPAddress, PhysicalAddress> _mappings = [];

        void ILocalAddressMapping.Update(IPAddress ip, PhysicalAddress mac)
        {
            _mappings[ip] = mac;

            local.Update(ip, mac);
        }

        void ILocalAddressMapping.Delete(IPAddress ip)
        {
            if (_mappings.TryRemove(ip, out _))
            {
                local.Delete(ip);
            }
        }

        void IDisposable.Dispose()
        {
            if (_mappings.Keys.Count > 0)
            {
                Logger.LogWarning("Removing {Count} leftover static IP address mapping(s):", _mappings.Keys.Count);

                foreach (var ip in _mappings.Keys)
                {
                    if (_mappings.TryRemove(ip, out _))
                    {
                        local.Delete(ip);
                    }
                }
            }
        }
    }
}
