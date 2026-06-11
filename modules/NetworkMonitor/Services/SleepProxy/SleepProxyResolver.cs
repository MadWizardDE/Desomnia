using MadWizard.Desomnia.Network.Naming;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.SleepProxy.Registration;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;

namespace MadWizard.Desomnia.Network.SleepProxy
{
    /// <summary>
    /// Answers DNS-SD browse requests the way an Apple Bonjour Sleep Proxy (BSP) would: it advertises
    /// the proxy service itself (<c>_sleep-proxy._udp.local</c>) and the services that watched hosts
    /// have asked us to advertise on their behalf while they are asleep / unreachable.
    /// </summary>
    internal class SleepProxyResolver(NetworkHost proxy, SleepProxyService service) : DNSService(service.Port), IMulticastDNSResolver
    {
        public required ILogger<SleepProxyResolver> Logger { private get; init; }

        public required SleepProxyRegistrar Registrar { private get; init; }

        void IMulticastDNSResolver.Resolve(DNSQuery query)
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

        /// <summary>
        /// Handles a Sleep Proxy registration: a DNS UPDATE whose OPT record carries an EDNS0 Owner option.
        /// The records to defend are in the UPDATE (authority) section; the wake info is in the Owner option.
        /// </summary>
        protected override void ProcessUpdate(DNSUpdate update)
        {
            try
            {
                var registration = ((SleepProxyRegistration)update.Request);

                Logger.LogInformation("Received a sleep-proxy registration for {MAC}.", registration.PrimaryAddress);

                var lease = Registrar.Register(registration);

                update.GrantLease(lease.Duration);

                RespondTo(update);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not handle sleep-proxy registration");

                try
                {
                    update.AnswerWithError(ex);

                    RespondTo(update);
                }
                catch (Exception error)
                {
                    Logger.LogError(error, "Could not send sleep-proxy error response");
                }
            }
        }
    }
}
