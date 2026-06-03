using MadWizard.Desomnia.Network.Naming.MDNS;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Services.SleepProxy;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.SleepProxy
{
    /// <summary>
    /// Answers DNS-SD browse requests the way an Apple Bonjour Sleep Proxy (BSP) would: it advertises
    /// the proxy service itself (<c>_sleep-proxy._udp.local</c>) and the services that watched hosts
    /// have asked us to advertise on their behalf while they are asleep / unreachable.
    /// </summary>
    /// <remarks>
    /// First draft. It answers from what we already know (configured services on watched hosts); it does
    /// not yet accept dynamic delegations (the DNS Update + EDNS0 Owner option a real client sends before
    /// sleeping), nor does it craft the magic packet on demand. See the TODOs below.
    /// </remarks>
    internal class SleepProxyResolver(NetworkHost proxy, SleepProxyService service) : IMulticastDNSResolver
    {
        public required ILogger<SleepProxyResolver> Logger { private get; init; }

        void IMulticastDNSResolver.Resolve(MulticastDNSQuery query)
        {
            foreach (var question in query.Questions.Where(q => q.Type is (DnsType.PTR or DnsType.ANY)))
            {
                if (question.Name == service.LocalDomainName)
                {
                    DomainName instance = new([$"{service.Metrics} {proxy.Name}", .. service.LocalDomainName.Labels]);

                    query.AnswerWith(proxy, service, instance);
                }
            }
        }
    }
}
