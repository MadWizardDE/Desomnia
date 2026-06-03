using Makaretu.Dns;
using System.Net;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.SleepProxy
{
    /// <summary>
    /// A parsed Sleep Proxy registration (a DNS UPDATE plus its EDNS0 Owner option): the records a sleeping
    /// host asked us to keep alive on its behalf, and how to wake it again. Handed to <see cref="ISleepProxyRegistrar"/>.
    /// </summary>
    public class SleepProxyRegistration
    {
        /// <summary>The records the host wants the proxy to hold and defend (the UPDATE section of the message).</summary>
        public required IReadOnlyList<ResourceRecord> Records { get; init; }

        /// <summary>The MAC to send the Wake-on-LAN magic packet to.</summary>
        public required PhysicalAddress WakeMac { get; init; }

        /// <summary>Optional SecureOn Wake-on-LAN password.</summary>
        public byte[]? Password { get; init; }

        /// <summary>The Owner option's sequence number (the host increments it on each registration).</summary>
        public byte Sequence { get; init; }

        /// <summary>The address the registration was sent from — i.e. the sleeping host itself.</summary>
        public required IPAddress ClientAddress { get; init; }

        /// <summary>The MAC the registration was sent from.</summary>
        public required PhysicalAddress ClientPhysicalAddress { get; init; }

        /// <summary>The lease duration the host requested, or <see cref="TimeSpan.Zero"/> if it didn't specify one.</summary>
        public TimeSpan RequestedLease { get; init; }
    }

    /// <summary>
    /// Consumes Sleep Proxy registrations. Implement this to take ownership of a sleeping host on the proxy's
    /// behalf — e.g. materialise a dynamic host/watch and wire up waking — and to grant the registration's lease.
    /// </summary>
    public interface ISleepProxyRegistrar
    {
        /// <summary>
        /// Accepts <paramref name="registration"/> and returns the granted lease duration,
        /// or <see cref="TimeSpan.Zero"/> to decline it.
        /// </summary>
        TimeSpan Register(SleepProxyRegistration registration);
    }
}
