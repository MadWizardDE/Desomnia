namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct WatchOptions
    {
        public WatchMode    Mode            { get; init; }
        public TimeSpan?    Timeout         { get; init; }
        public ushort[]     UDPPorts        { get; init; }
        public bool         Handoff         { get; init; }
    }

    public enum WatchMode
    {
        None = 0,

        Normal,
        Promiscuous
    }
}
