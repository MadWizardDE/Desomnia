namespace MadWizard.Desomnia.Network.Neighborhood.Options
{
    public struct ServiceOptions
    {
        public IPAddressFlags Flags { get; set; }
        public DateTime? Expires { get; set; }

        public ServiceOptions(IPAddressFlags flags)
        {
            Flags = flags;
        }

        public ServiceOptions(TimeSpan? lifetime)
        {
            if (lifetime.HasValue)
            {
                Expires = DateTime.Now + lifetime;
            }
        }

        public readonly bool HasFlags(IPAddressFlags flags) => Flags.HasFlag(flags);
    }

    public enum NetworkServiceFlags
    {
        None = 0,

        Static = 1 << 0,
    }
}
