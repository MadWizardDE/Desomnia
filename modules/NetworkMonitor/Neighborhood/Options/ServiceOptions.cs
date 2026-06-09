namespace MadWizard.Desomnia.Network.Neighborhood.Options
{
    public struct ServiceOptions
    {
        public ServiceFlags Flags { get; set; }
        public DateTime? Expires { get; set; }

        public ServiceOptions(ServiceFlags flags)
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

        public readonly bool HasFlags(ServiceFlags flags) => Flags.HasFlag(flags);
    }

    public enum ServiceFlags
    {
        None = 0,

        Static = 1 << 0,
    }
}
