using MadWizard.Desomnia.Network.Configuration.Services;
using MadWizard.Desomnia.Network.Naming.Options;
using MadWizard.Desomnia.Network.Neighborhood.Options;
using Makaretu.Dns;
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
        public byte             Version         { get; init; }
        /// <summary>The Owner option's sequence number (the host increments it on each registration).</summary>
        public byte             Sequence        { get; init; }

        public PhysicalAddress  PhysicalAddress { get; init; }
        public PhysicalAddress? TargetAddress   { get; init; }
        /// <summary>Optional SecureOn Wake-on-LAN password.</summary>
        public byte[]?          Password        { get; init; }

        /// <summary>Optional lease duration, from EDS0 Lease option </summary>
        public TimeSpan?        RequestedLease  { get; init; }

        // extracted from records:

        public required string                          Name        { get; set; }
        public required string                          Hostname    { get; set; }
        public Dictionary<IPAddress, IPAddressOptions>  IPAddresses { get; init; } = [];
        public List<SleepProxyServiceInfo>              Services    { get; init; } = [];

        public SleepProxyRegistration(EdnsOwnerOption owner, EdnsLeaseOption? lease)
        {
            Version = owner.Version;
            Sequence = owner.Sequence;

            PhysicalAddress = owner.PrimaryMac;
            TargetAddress = owner.WakeupMac;
            Password = owner.Password;

            RequestedLease = lease?.Duration;
        }
    }

    public class SleepProxyServiceInfo : ServiceInfo
    {
        // TODO wie mappen wir das?
        public ushort Priority  { get; set; }
        public ushort Weight    { get; set; }

        public List<string> TextRecords { get; init; } = [];

        internal static SleepProxyServiceInfo ParsePTR(PTRRecord ptr)
        {
            string serviceName = ptr.ServiceName;

            return new()
            {
                Name = DeriveName(serviceName),
                ServiceName = serviceName,

                Protocol = ptr.Protocol,

                AdvertiseServiceTTL = ptr.TTL
            };
        }

        private static string DeriveName(string serviceName)
        {
            return serviceName; // TODO dedizierte Namensableitung?
        }
    }
}
