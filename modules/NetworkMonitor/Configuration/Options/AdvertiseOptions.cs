using System.Net;
using System.Net.Sockets;

namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct AdvertiseOptions
    {
        public AdvertiseType    Type    { get; init; }
        public TimeSpan         Timeout { get; init; }

        public bool ShouldAdvertiseOnRemoteHostDemand(IPAddress ip)     => Type.HasFlag(AdvertiseType.Demand)  && ShouldAdvertise(ip);
        public bool ShouldAdvertiseOnRemoteHostSuspended(IPAddress ip)  => Type.HasFlag(AdvertiseType.Suspend) && ShouldAdvertise(ip);
        public bool ShouldAdvertiseOnRemoteHostStopped(IPAddress ip)    => Type.HasFlag(AdvertiseType.Stop)    && ShouldAdvertise(ip);
        public bool ShouldAdvertiseOnLocalHostResume(IPAddress ip)      => Type.HasFlag(AdvertiseType.Resume)  && ShouldAdvertise(ip);

        private readonly bool ShouldAdvertise(IPAddress ip)
        {
            switch (ip.AddressFamily)
            {
                case AddressFamily.InterNetwork when Type.HasFlag(AdvertiseType.IPv4):
                    return true;
                case AddressFamily.InterNetworkV6 when Type.HasFlag(AdvertiseType.IPv6):
                    return true;

                default:
                    return false;
            }
        }

    }

    [Flags]
    public enum AdvertiseType
    {
        Never = 0,

        IPv4 = 1 << 1,
        IPv6 = 1 << 2,

        IP = IPv4 | IPv6,

        Hostname = 1 << 5,
        Services = 1 << 6,

        Demand = 1 << 10, // advertise IPs when remote host is requested
        Suspend = 1 << 11, // advertise IPs after the remote host has been suspended
        Stop = 1 << 12, // advertise IPs after the remote host has been stopped (manually or on disconnect)

        Resume = 1 << 15, // advertise IPs when the local host resumes from suspend

        Lazy = IP | Demand,
        Eager = IP | Demand | Suspend | Resume
    }
}
