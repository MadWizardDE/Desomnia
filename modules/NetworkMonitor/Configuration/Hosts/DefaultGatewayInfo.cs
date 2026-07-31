using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Manager;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Configuration.Hosts
{
    internal class DefaultGatewayInfo(INetworkInterface ni, AutoDiscoveryType auto) : NetworkRouterInfo
    {
        internal async Task TryLookupGatewayName()
        {
            foreach (var ip in IPAddresses)
            {
                if (await ip.LookupName() is string name)
                {
                    Name = name;
                    AutoDetect = auto;
                    break;
                }
            }
        }

        public override IEnumerable<IPAddress> IPAddresses
        {
            get
            {
                foreach (var gateway in ni.Gateways)
                {
                    var address = gateway.Address.RemoveScopeId();

                    if (address.AddressFamily == AddressFamily.InterNetwork)
                        if (auto.HasFlag(AutoDiscoveryType.IPv4))
                            yield return address;

                    if (address.AddressFamily == AddressFamily.InterNetworkV6)
                        if (auto.HasFlag(AutoDiscoveryType.IPv6))
                            yield return address;
                }
            }
        }
    }
}
