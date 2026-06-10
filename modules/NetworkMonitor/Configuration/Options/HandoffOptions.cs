namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct HandoffOptions
    {
        public readonly HandoffType Type            { get; init; }

        public readonly TimeSpan    Timeout         { get; init; }
        public readonly TimeSpan?   LeaseDuration   { get; init; }

        public readonly byte[]?     Password        { get; init; } // TODO implement in config

        public bool IsRequired => Type.HasFlag(HandoffType.Required);
    }

    [Flags]
    public enum HandoffType
    {
        None            = 0,

        SleepProxy      = 1 << 1,
        UnMagicPacket   = 1 << 2,

        Required        = 1 << 10,
    }
}
