using MadWizard.Desomnia.Network.Naming;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Options;
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
                var owner = update.Owner ?? throw new FormatException("DNS UPDATE without an EDNS0 Owner option");

                //Logger.LogInformation("Received a sleep-proxy registration for {}", owner.PrimaryMac);

                // TODO Zone = ".local" prüfen

                ValidateNames(update.Request.AuthorityRecords, out string name, out string hostname);

                var registration = new SleepProxyRegistration(name, hostname, owner, update.Lease);

                // read IP addresses
                foreach (var adr in update.Request.AuthorityRecords.OfType<AddressRecord>())
                    registration.IPAddresses[adr.Address] = new(IPAddressFlags.Static)
                    {
                        TTL = adr.TTL,
                    };

                registration.Services.AddRange(ReadServices(update.Request.AuthorityRecords));

                var lease = Registrar.Register(registration);

                update.GrantLease(lease.Duration);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not handle sleep-proxy registration"); // TODO send error message?
            }
        }

        #region Reader for DNS data
        void ValidateNames(IEnumerable<ResourceRecord> records, out string name, out string hostname)
        {
            static void Validate(ref string name, string str)
            {
                if (name == string.Empty)
                    name = str;

                else if (name != str)
                {
                    throw new FormatException($"'{name}' != '{str}'");
                }
            }

            name = string.Empty;
            hostname = string.Empty;
            foreach (var record in records)
            {
                switch (record)
                {
                    case PTRRecord ptr:
                        Validate(ref name,      ptr.InstanceName);
                        break;

                    case SRVRecord srv:
                        Validate(ref name,      srv.InstanceName);
                        Validate(ref hostname,  srv.HostName);
                        break;

                    case AddressRecord adr:
                        Validate(ref hostname,  adr.HostName);
                        break;
                }
            }

            if (name == string.Empty)
                throw new FormatException("Could not determine instance name");
            if (hostname == string.Empty)
                throw new FormatException("Could not determine host name");
        }

        IEnumerable<ProxyServiceInfo> ReadServices(IEnumerable<ResourceRecord> records)
        {
            var services = records.OfType<PTRRecord>().Select(ProxyServiceInfo.ParsePTR).ToDictionary(info => info.ServiceName!);

            foreach (var record in records)
            {
                switch (record)
                {
                    case SRVRecord srv when services[srv.ServiceName] is var service:
                        if (service.Protocol != srv.Protocol)
                            throw new FormatException($"Protocol mismatch @ '{srv.ServiceName}': {service.Protocol} != {srv.Protocol}");
                        service.Port = srv.Port;
                        service.Priority = srv.Priority;
                        service.Weight = srv.Weight;
                        service.AdvertiseHostTTL = srv.TTL;
                        break;

                    case TXTRecord txt when services[txt.ServiceName] is var service:
                        service.TextRecords.AddRange(txt.Strings);
                        break;
                }
            }

            return services.Values;
        }
        #endregion
    }
}
