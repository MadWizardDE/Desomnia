using MadWizard.Desomnia.Network.Naming.MDNS;
using MadWizard.Desomnia.Network.Neighborhood.Services;
using System.Net;

namespace MadWizard.Desomnia.Network.SleepProxy
{
    internal class SleepProxyService(SleepProxyMetrics metrics) : TransportNetworkService("SleepProxy", new IPPort(IPProtocol.UDP, MulticastDNSService.MulticastPort))
    {
        public override string ServiceName => "sleep-proxy";

        public SleepProxyMetrics Metrics { get; set; } = metrics;
    }
}
