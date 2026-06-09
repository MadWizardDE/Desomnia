namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct WakeOptions
    {
        public WakeType     Type        { get; init; }
        public ushort       Port        { get; init; }

        public byte[]?      Password    { get; init; }

        public TimeSpan     Timeout     { get; init; }
        public TimeSpan?    Repeat      { get; init; }

        public bool         Ping        { get; init; }
        public bool         Silent      { get; init; }
    }

    [Flags]
    public enum WakeType
    {
        None        = 0,

        Link        = 1 << 1,
        Network     = 1 << 2,

        Unicast     = 1 << 10,
        Broadcast   = 1 << 11,

        Auto        = 1 << 20,
    }
}
