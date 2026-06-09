namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct HandoffOptions
    {
        public readonly HandoffType Type { get; init; }

        public readonly TimeSpan Timeout { get; init; }
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
