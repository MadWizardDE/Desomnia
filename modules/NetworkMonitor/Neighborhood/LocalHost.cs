using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Neighborhood
{
    internal class LocalHost(NetworkDevice device) : NetworkHost(Dns.GetHostName())
    {
        public override string Name => Dns.GetHostName();

        public override PhysicalAddress? PhysicalAddress => device.PhysicalAddress;

        public override IEnumerable<IPAddress> IPAddresses => device.IPAddresses;

        public override bool AddAddress(IPAddress ip, TimeSpan? lifetime = null, IPAddressFlags flags = IPAddressFlags.None) => throw new InvalidOperationException("LocalHost");
        public override bool RemoveAddress(IPAddress ip, bool expired = false) => throw new InvalidOperationException("LocalHost");

        public override bool ShouldAddressExpire(IPAddress ip, out DateTime expires, out IPAddressFlags flags)
        {
            expires = DateTime.MaxValue;

            flags = default;

            return false;
        }
    }
}