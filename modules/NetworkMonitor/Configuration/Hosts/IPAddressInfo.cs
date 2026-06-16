using System.Net;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Configuration.Hosts
{
    public class IPAddressInfo
    {
        public IPAddress? IPv4 { get; set; }
        public IPAddress? IPv6 { get; set; }

        public IPAddressInfo() { }

        internal IPAddressInfo(IPAddress ip)
        {
            switch (ip.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    IPv4 = ip;
                    break;
                case AddressFamily.InterNetworkV6:
                    IPv6 = ip;
                    break;
            }
        }

        public virtual IEnumerable<IPAddress> IPAddresses
        {
            get
            {
                if (IPv4 != null)
                    yield return IPv4;
                if (IPv6 != null)
                    yield return IPv6;
            }
        }
    }
}
