using MadWizard.Desomnia.Network.Configuration.Filter;
using MadWizard.Desomnia.Network.Configuration.Options;
using MadWizard.Desomnia.Network.Configuration.Services;
using MadWizard.Desomnia.Network.Naming.Options;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using Makaretu.Dns;
using NetTools;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.SleepProxy.Registration
{
    /// <summary>
    /// A parsed Sleep Proxy registration (a DNS UPDATE plus its EDNS0 Owner option): the records a sleeping
    /// host asked us to keep alive on its behalf, and how to wake it again.
    /// </summary>
    public class SleepProxyRegistration
    {
        public byte             Version         { get; init; } = 0;
        /// <summary>The Owner option's sequence number (the host increments it on each registration).</summary>
        public byte             Sequence        { get; init; } = 0;

        public PhysicalAddress  PrimaryAddress  { get; init; }
        public PhysicalAddress? TargetAddress   { get; init; }
        /// <summary>Optional SecureOn Wake-on-LAN password.</summary>
        public byte[]?          Password        { get; init; }

        /// <summary>Optional lease duration, from EDS0 Lease option </summary>
        public TimeSpan         RequestedLease  { get; init; }

        // extracted from records:

        public string                                   Name        { get; init; }
        public string                                   Hostname    { get; init; }
        public Dictionary<IPAddress, IPAddressOptions>  IPAddresses { get; init; } = [];
        public List<ProxyServiceInfo>                   Services    { get; init; } = [];

        private SleepProxyRegistration(NetworkHost host)
        {
            Name = host.Name;
            Hostname = host.HostName;

            PrimaryAddress = host.PhysicalAddress ?? throw new NotSupportedException($"Host {host.Name} has not MAC address configured.");

            if (host is VirtualNetworkHost virtualHost)
            {
                TargetAddress = virtualHost.PhysicalHost.PhysicalAddress;
            }
        }

        public SleepProxyRegistration(NetworkHost host, HandoffOptions options, byte sequence) : this(host)
        {
            foreach (var ip in host.SelectIPAddressesBy(options))
            {
                IPAddresses[ip] = host[ip];
            }

            RequestedLease = options.Duration;
            Password = options.Password;

            Sequence = sequence;
        }

        public SleepProxyRegistration(string name, string hostname, EdnsOwnerOption owner, EdnsLeaseOption lease)
        {
            Name = name;
            Hostname = hostname;

            Version = owner.Version;
            Sequence = owner.Sequence;

            PrimaryAddress = owner.PrimaryMac;
            TargetAddress = owner.WakeupMac;
            Password = owner.Password;

            RequestedLease = lease.Duration;
        }

        internal IEnumerable<EdnsOption> Options
        {
            get
            {
                yield return new EdnsOwnerOption
                {
                    Version = Version,
                    Sequence = Sequence,
                    PrimaryMac = PrimaryAddress,
                    WakeupMac = TargetAddress,
                    Password = Password,
                };

                yield return new EdnsLeaseOption { Duration = RequestedLease };

                foreach (var service in Services)
                {
                    var option = new EdnsServiceFilterOption { ServiceDomainName = service.Service.LocalDomainName };

                    foreach (var host in service.HostFilterRule)
                    {
                        if (host.IsDynamic)
                            option.Filters.Add(new DynamicHostFilterEntry   { Type = host.Type, Name = host.Name! });
                        else foreach(var ip in host.IPAddresses)
                            option.Filters.Add(new StaticHostFilterEntry    { Type = host.Type, Address = ip });
                    }

                    foreach (var range in service.HostRangeFilterRule)
                    {
                        if (range is LocalRangeFilterRuleInfo)
                            option.Filters.Add(new LocalRangeFilterEntry    { Type = range.Type });
                        else if (range.AddressRange is IPAddressRange addressRange)
                            option.Filters.Add(new StaticRangeFilterEntry   { Type = range.Type, Range = addressRange });
                    }

                    if (option.Filters.Count > 0)
                        yield return option;
                }
            }
        }

        /// <summary>
        /// Whether <paramref name="other"/> describes substantially the same registration as this one: the same host
        /// identity (MAC, wake target, names) defending the same addresses and services. The transient negotiation
        /// fields (message <see cref="Id"/>, <see cref="Sequence"/>, requested lease, password) are intentionally ignored.
        /// </summary>
        internal bool Matches(SleepProxyRegistration other)
        {
            return Equals(Name, other.Name)
                && Equals(Hostname, other.Hostname)
                && Equals(PrimaryAddress, other.PrimaryAddress)
                && Equals(TargetAddress, other.TargetAddress)
                && SamePassword(Password, other.Password)
                && IPAddresses.Keys.ToHashSet().SetEquals(other.IPAddresses.Keys)
                && ServiceSignatures().SetEquals(other.ServiceSignatures());
        }

        // The password is a fresh byte[] on every parse, so it must be compared by content, not reference.
        private static bool SamePassword(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);

        private HashSet<string> ServiceSignatures() => [.. Services.Select(s => $"{s.InstanceName}|{s.Service.LocalDomainName}|{s.Port}")];

        public static explicit operator SleepProxyRegistration(Message message) => SleepProxyRegistrationFormat.ParseUpdateMessage(message);
        public static explicit operator Message(SleepProxyRegistration reg)     => SleepProxyRegistrationFormat.BuildUpdateMessage(reg);

    }

    public class ProxyServiceInfo : WatchedServiceInfo
    {
        // TODO: wie mappen wir das?
        public ushort Priority  { get; set; }
        public ushort Weight    { get; set; }

        // TODO: wie mappen wir TXT records?

        public ProxyServiceInfo()
        {
            AdvertiseTimeout = TimeSpan.Zero; // proxied services should be answered immediately
        }

        public ProxyServiceInfo(AdvertiseOptions options) : this()
        {
            AdvertiseHostTTL = options.HostTTL;
            AdvertiseServiceTTL = options.ServiceTTL;
        }
    }
}
