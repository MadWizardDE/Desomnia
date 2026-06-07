namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct DiscoveryOptions
    {
        public TimeSpan Timeout { get; init; }
        public TimeSpan? Refresh { get; init; }
    }

    [Flags]
    public enum AutoDiscoveryType
    {
        Nothing     = 0,

        MAC         = 1 << 1,

        IPv4        = 1 << 2,
        IPv6        = 1 << 3,

        IP          = IPv4 | IPv6,

        Router      = 1 << 10,
        VPN         = 1 << 11,

        Host        = 1 << 15,
        Service     = 1 << 16,

        SleepProxy  = 1 << 20,

        Everything = MAC | IP | Router | VPN | SleepProxy | Service
    }
}
