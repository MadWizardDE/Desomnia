using MadWizard.Desomnia.Network.Naming.Options;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using Makaretu.Dns;

namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    internal static class SleepProxyRegistrationFormat
    {
        private static readonly DomainName LocalZone = new("local");

        // Fallback TTLs when a service has no advertise TTL configured (RFC 6762 §10).
        private static readonly TimeSpan HostRecordTTL = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan ServiceRecordTTL = TimeSpan.FromMinutes(75);

        #region Read from DNS update
        internal static SleepProxyRegistration ParseUpdateMessage(Message message)
        {
            // TODO Zone = ".local" prüfen

            var lease = message.Options.OfType<EdnsLeaseOption>().FirstOrDefault();
            if (message.Options.OfType<EdnsOwnerOption>().FirstOrDefault() is not EdnsOwnerOption owner)
                throw new FormatException("DNS UPDATE without an EDNS0 Owner option");

            ValidateNames(message.AuthorityRecords, out string name, out string hostname);

            var reg = new SleepProxyRegistration(name, hostname, owner, lease);

            // read IP addresses
            foreach (var adr in message.AuthorityRecords.OfType<AddressRecord>())
                reg.IPAddresses[adr.Address] = new(IPAddressFlags.Static)
                {
                    TTL = adr.TTL,
                };

            reg.Services.AddRange(ReadServices(message.AuthorityRecords));

            return reg;
        }

        static void ValidateNames(IEnumerable<ResourceRecord> records, out string name, out string hostname)
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
                        Validate(ref name, ptr.InstanceName);
                        break;

                    case SRVRecord srv:
                        Validate(ref name, srv.InstanceName);
                        Validate(ref hostname, srv.HostName);
                        break;

                    case AddressRecord adr:
                        Validate(ref hostname, adr.HostName);
                        break;
                }
            }

            if (name == string.Empty)
                throw new FormatException("Could not determine instance name");
            if (hostname == string.Empty)
                throw new FormatException("Could not determine host name");
        }

        static IEnumerable<ProxyServiceInfo> ReadServices(IEnumerable<ResourceRecord> records)
        {
            var services = records.OfType<PTRRecord>().Select(ProxyServiceInfo.ParsePTR).ToDictionary(info => info.ServiceName!);

            try
            {
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
                            service.ParseTXT(txt);
                            break;
                    }
                }
            }
            catch (KeyNotFoundException ex)
            {
                throw new FormatException("PTR record missing", ex);
            }

            return services.Values;
        }
        #endregion

        #region Write to DNS update message
        internal static Message BuildUpdateMessage(SleepProxyRegistration reg)
        {
            var message = new Message
            {
                Id = (ushort)Random.Shared.Next(1, ushort.MaxValue),
                Opcode = MessageOperation.Update,
            };

            // Zone section (RFC 2136): the ".local" zone.
            message.Questions.Add(new Question { Name = LocalZone, Type = DnsType.SOA, Class = DnsClass.IN });

            DomainName host = new([reg.Hostname, .. LocalZone.Labels]);         // host.local

            // Address records (A / AAAA).
            foreach (var (ip, options) in reg.IPAddresses)
            {
                var record = AddressRecord.Create(host, ip);
                record.TTL = options.TTL ?? HostRecordTTL;
                message.AuthorityRecords.Add(record);
            }

            // Service records: PTR (service type -> instance) + SRV (instance -> host:port) + TXT.
            foreach (var info in reg.Services)
            {
                DomainName serviceType = info.Service.LocalDomainName;          // _svc._proto.local
                DomainName instance = new([reg.Name, .. serviceType.Labels]);   // <name>._svc._proto.local

                message.AuthorityRecords.Add(new PTRRecord
                {
                    Name = serviceType,
                    DomainName = instance,
                    TTL = info.AdvertiseServiceTTL ?? ServiceRecordTTL,
                });

                message.AuthorityRecords.Add(new SRVRecord
                {
                    Name = instance,
                    Target = host,
                    Port = info.Port,
                    Priority = info.Priority,
                    Weight = info.Weight,
                    TTL = info.AdvertiseHostTTL ?? HostRecordTTL,
                });

                message.AuthorityRecords.Add(new TXTRecord
                {
                    Name = instance,
                    Strings = [.. info.Select(entry => $"{entry.Key}={entry.Value}")],
                    TTL = info.AdvertiseHostTTL ?? HostRecordTTL,
                });
            }

            // OPT with the Owner option (how to wake us) and the requested lease.
            message.AdditionalRecords.Add(new OPTRecord()
            {
                Options = [.. reg.Options]
            });

            return message;
        }
        #endregion
    }
}
