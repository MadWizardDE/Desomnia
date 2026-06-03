using MadWizard.Desomnia.Network.Naming.MDNS;
using MadWizard.Desomnia.Network.Naming.Options;
using MadWizard.Desomnia.Network.Neighborhood;
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

        /// <summary>
        /// Handles a Sleep Proxy registration: a DNS UPDATE whose OPT record carries an EDNS0 Owner option.
        /// The records to defend are in the UPDATE (authority) section; the wake info is in the Owner option.
        /// </summary>
        void IMulticastDNSResolver.Update(MulticastDNSUpdate update)
        {
            try
            {
                if (update.Owner is not EdnsOwnerOption owner)
                {
                    Logger.LogTrace("Ignoring DNS UPDATE without an EDNS0 Owner option (not a sleep-proxy registration).");

                    return;
                }

                var registration = new SleepProxyRegistration
                {
                    Records = update.AuthorityRecords,
                    WakeMac = owner.WakeTarget,
                    Password = owner.Password,
                    Sequence = owner.Sequence,
                    ClientAddress = update.SourceIPAddress,
                    ClientPhysicalAddress = update.SourcePhysicalAddress,
                    RequestedLease = update.Lease?.Duration ?? TimeSpan.Zero,
                };

                TimeSpan granted = TimeSpan.MaxValue;

                if (granted > TimeSpan.Zero)
                {
                    update.GrantLease(granted);
                }
                else
                    Logger.LogTrace("No registrar accepted the sleep-proxy registration from {ip}.", update.SourceIPAddress);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to handle sleep-proxy registration");
            }

        }
    }
}
