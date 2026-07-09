namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct SleepProxyOptions
    {
        public int          Limit                   { get; init; }

        public TimeSpan     MinLeaseDuration        { get; init; }
        public TimeSpan     MaxLeaseDuration        { get; init; }

        public LeaseExpireAction ExpireLease        { get; init; }

        public TimeSpan DetermineLeaseDuration(TimeSpan requestedDuration)
        {
            if (requestedDuration < MinLeaseDuration)
                return MinLeaseDuration;
            else if (requestedDuration > MaxLeaseDuration)
                return MaxLeaseDuration;
            else
                return requestedDuration;
        }
    }

    public enum SleepProxyDiscoveryType
    {
        None    = 0,

        Eager   = 1 << 1,
        Lazy    = 1 << 2,

        Fast    = 1 << 5
    }

    public enum LeaseExpireAction
    {
        None = 0,

        Wake = 1
    }
}
