using MadWizard.Desomnia.Network.SleepProxy;
using System.Net;

namespace MadWizard.Desomnia.Network.Configuration.Services
{
    internal class SleepProxyServiceInfo : ServiceInfo
    {
        public SleepProxyServiceInfo(ushort port = 5353)
        {
            Name = "SleepProxy";
            ServiceName = "sleep-proxy";
            Protocol = IPProtocol.UDP;
            Port = port;
        }

        internal SleepProxyMetrics Metrics { get; set; } = SleepProxyMetrics.Best;

        public override SleepProxyService Service => new(Port)
        {
            Name = Name,
            ServiceName = ServiceName!
        };
    }
}
