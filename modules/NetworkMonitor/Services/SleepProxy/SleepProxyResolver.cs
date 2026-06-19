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
    internal class SleepProxyResolver(NetworkHost proxy, SleepProxyService service) : GuardedDNSService(service.Port), IMulticastDNSResolver
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
            var reg = ((SleepProxyRegistration)update.Request);

            try
            {
                if (Registrar.Register(reg, out var lease))
                {
                    Logger.LogDebug("Registration of '{Name}' successful; granting lease: {Duration}", reg.Name, lease.Duration);

                    update.AnswerWithLease(lease.Duration);

                    lease.Ended += (sender, args) =>
                    {
                        // TODO: Warum wird das manchmal zum Host gelogged??
                        Logger.LogDebug("Lease for '{Name}' has {Verb}", reg.Name, args.HasExpired ? "expired" : args.HasFailed ? "failed" : "ended");
                    };
                }
                else // no new registration was created, we merely confirm the existing one
                {
                    update.AnswerWithLease(lease.GrantedUntil - DateTime.Now);
                }

                RespondTo(update);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Registration of '{Name}' failed.", reg.Name);

                update.AnswerWithError(ex);

                RespondTo(update);
            }
        }
    }
}
