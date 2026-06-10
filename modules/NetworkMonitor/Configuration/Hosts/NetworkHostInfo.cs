using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Configuration.Services;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Configuration.Hosts
{
    public class NetworkHostInfo : IPAddressInfo
    {
        public AutoDiscoveryType? AutoDetect { get; set; }

        public required string Name { get; set; }
        public string? HostName { get; set; }

        public PhysicalAddress? MAC { get; set; }

        public bool Trace { get; set; } = false;

        #region Services
        private IList<ServiceInfo> Service { get; set; } = [];
        private SleepProxyServiceInfo? SleepProxyService { get; set; }

        public virtual IEnumerable<ServiceInfo> Services 
        {
            get
            {
                foreach (var service in Service)
                    yield return service;

                if (SleepProxyService != null)
                    yield return SleepProxyService;
            }
        }
        #endregion
    }
}
