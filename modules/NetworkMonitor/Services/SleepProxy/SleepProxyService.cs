using MadWizard.Desomnia.Network.Neighborhood.Services;
using System.Net;

namespace MadWizard.Desomnia.Network.SleepProxy
{
    internal class SleepProxyService(ushort port) : TransportNetworkService("SleepProxy", new IPPort(IPProtocol.UDP, port))
    {
        public override string ServiceName => "sleep-proxy";

        public SleepProxyMetrics Metrics { get; set; }
    }
}
