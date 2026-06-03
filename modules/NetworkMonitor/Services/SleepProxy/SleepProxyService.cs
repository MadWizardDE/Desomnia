using MadWizard.Desomnia.Network.Naming.MDNS;
using MadWizard.Desomnia.Network.Neighborhood.Services;
using MadWizard.Desomnia.Network.SleepProxy;
using System.Net;

namespace MadWizard.Desomnia.Network.Services.SleepProxy
{
    internal class SleepProxyService(SleepProxyMetrics metrics) : TransportNetworkService("SleepProxy", new IPPort(IPProtocol.UDP, MulticastDNSService.MulticastPort))
    {
        public override string ServiceName => "sleep-proxy";

        public SleepProxyMetrics Metrics { get; set; } = metrics;
    }
}
