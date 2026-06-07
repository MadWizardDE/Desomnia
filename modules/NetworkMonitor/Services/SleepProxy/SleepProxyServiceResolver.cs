using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Naming.MDNS;
using MadWizard.Desomnia.Network.Neighborhood.Services;
using MadWizard.Desomnia.Network.Watch;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.SleepProxy
{
    internal class SleepProxyServiceResolver : IMulticastDNSResolver
    {
        public required ILogger<SleepProxyServiceResolver> Logger { private get; init; }

        public required NetworkMonitor Monitor { private get; init; }

        void IMulticastDNSResolver.Resolve(MulticastDNSQuery query)
        {
            foreach (var question in query.Questions.Where(q => q.Type is (DnsType.PTR or DnsType.ANY)))
            {
                if (question.QU)
                    continue;

                foreach (HostDemandWatch watch in Monitor.OfType<HostDemandWatch>()) foreach (NetworkServiceWatch serviceWatch in watch)
                {
                    if (serviceWatch.AdvertiseOptions?.Type.HasFlag(AdvertiseType.Service) ?? false) // should we answer for this service?
                    {
                        if (serviceWatch.Service is TransportNetworkService service && service.LocalDomainName == question.Name)
                        {
                            query.AnswerWith(watch.Host, service, options: new(watch.AdvertiseOptions));
                        }
                    }
                }
            }
        }
    }
}
