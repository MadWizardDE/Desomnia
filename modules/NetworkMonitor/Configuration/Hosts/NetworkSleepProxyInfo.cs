using MadWizard.Desomnia.Network.Configuration.Services;
using MadWizard.Desomnia.Network.SleepProxy;

namespace MadWizard.Desomnia.Network.Configuration.Hosts
{
    public class NetworkSleepProxyInfo : NetworkHostInfo
    {
        private ushort Port { get; set; } = 5353;

        private SleepProxyMetrics Metrics { get; set; } = SleepProxyMetrics.Best;

        public override IEnumerable<ServiceInfo> Services
        {
            get
            {
                yield return new SleepProxyServiceInfo(Port) { Name = "SleepProxy", Metrics = Metrics };
            }
        }
    }
}
