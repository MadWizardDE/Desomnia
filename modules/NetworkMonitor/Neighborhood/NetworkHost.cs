using MadWizard.Desomnia.Network.Neighborhood.Events;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Neighborhood
{
    public class NetworkHost(string name)
    {
        public virtual string Name { get; init; } = name;
        public virtual string HostName { get => field ?? Name; set; } = null!;

        public required NetworkSegment Network { get; init; }

        public virtual PhysicalAddress? PhysicalAddress { get; set { field = value;  PhysicalAddressChanged?.Invoke(this, new(value!)); } }

        readonly ConcurrentDictionary<IPAddress, IPAddressScope> _addresses = [];

        public virtual IEnumerable<IPAddress> IPAddresses => _addresses.Keys;
        public IEnumerable<IPAddress> IPv4Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        public IEnumerable<IPAddress> IPv6Addresses => IPAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);

        public event EventHandler<AddressAddedEventArgs>? AddressAdded;
        public event EventHandler<AddressRemovedEventArgs>? AddressRemoved;
        public event EventHandler<PhysicalAddressEventArgs>? PhysicalAddressChanged;

        public virtual bool AddAddress(IPAddress ip, TimeSpan? lifetime = null, IPAddressFlags flags = default)
        {
            ip.RemoveScopeId();

            var expires = lifetime != null ? DateTime.Now + lifetime : null;

            if (_addresses.ContainsKey(ip))
            {
                if (_addresses[ip].Expires < expires)
                {
                    _addresses[ip].Expires = expires;
                }

                return false;
            }
            else
            {
                _addresses[ip] = new()
                {
                    Flags = flags,
                    Expires = expires
                };

                AddressAdded?.Invoke(this, new(ip, expires));

                return true;
            }
        }

        public virtual bool ShouldAddressExpire(IPAddress ip, out DateTime expires, out IPAddressFlags flags)
        {
            expires = DateTime.MaxValue;

            if (_addresses.TryGetValue(ip, out var scope))
            {
                flags = scope.Flags;

                if (scope.Expires is DateTime date)
                {
                    expires = date;

                    return true;
                }

                return false;
            }

            throw new KeyNotFoundException("IP = " + ip.ToString());
        }

        public bool HasAddress(PhysicalAddress? mac = null, IPAddress? ip = null, bool both = false)
        {
            if (mac != null || ip != null)
            {
                bool hasMac = mac != null && mac.Equals(this.PhysicalAddress);
                bool hasIP = ip != null && this.IPAddresses.Contains(ip);

                return both ? hasMac && hasIP : hasMac || hasIP;
            }

            return false;
        }

        public virtual bool RemoveAddress(IPAddress ip, bool expired = false)
        {
            if (_addresses.Remove(ip, out _))
            {
                AddressRemoved?.Invoke(this, new(ip, expired));

                return true;
            }

            return false;
        }

        private class IPAddressScope
        {
            public IPAddressFlags   Flags   { get; set; }
            public DateTime?        Expires { get; set; }
        }
    }

    public enum IPAddressFlags
    {
        None = 0,

        Static  = 1 << 0,
    }
}
