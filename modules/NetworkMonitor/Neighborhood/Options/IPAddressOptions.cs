namespace MadWizard.Desomnia.Network.Neighborhood.Options
{
    public struct IPAddressOptions
    {
        public IPAddressFlags   Flags   { get; set; }

        public DateTime?        Expires { get; set; }
        public TimeSpan?        TTL     { get; set; }

        public IPAddressOptions(IPAddressFlags flags)
        {
            Flags = flags;
        }

        public IPAddressOptions(TimeSpan? lifetime)
        {
            if (lifetime.HasValue)
            {
                Expires = DateTime.Now + lifetime;
            }
        }

        public readonly bool HasFlags(IPAddressFlags flags) => Flags.HasFlag(flags);
    }

    public enum IPAddressFlags
    {
        None = 0,

        Static      = 1 << 0,
        Ephemeral   = 1 << 1
    }
}
