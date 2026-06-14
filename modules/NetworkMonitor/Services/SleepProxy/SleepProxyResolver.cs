using MadWizard.Desomnia.Network.Naming;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.SleepProxy.Registration;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

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
            Logger.LogDebug("Received a dynamic DNS update from {Endpoint}", update.SourceEndpoint);

            var registration = ((SleepProxyRegistration)update.Request);

            try
            {
                Logger.LogDebug("Attempt to register '{Name}' at {PhysicalAddress} with {ServiceCount} service(s) and {AddressCount} address(es)...",
                    registration.Name, registration.PrimaryAddress.ToHexString(), registration.Services.Count, registration.IPAddresses.Count);

                if (Registrar.Register(registration) is SleepProxyLease lease)
                {
                    Logger.LogDebug("Registration of '{Name}' successful; granting lease: {Duration}", registration.Name, lease.Duration);

                    update.AnswerWithLease(lease.Duration);

                    RespondTo(update);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Registration of '{Name}' failed.", registration.Name);

                update.AnswerWithError(ex);

                RespondTo(update);
            }
        }
    }
}
