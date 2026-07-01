namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct SleepProxyOptions
    {
        public TimeSpan     MinLeaseDuration        { get; init; }
        public TimeSpan?    DefaultLeaseDuration    { get; init; }
        public TimeSpan     MaxLeaseDuration        { get; init; }

        public LeaseExpireAction ExpireLease        { get; init; }

        public TimeSpan DetermineLeaseDuration(TimeSpan? requestedDuration, TimeSpan? defaultDuration = null)
        {
            if (requestedDuration < MinLeaseDuration)
                return MinLeaseDuration;
            else if (requestedDuration > MaxLeaseDuration)
                return MaxLeaseDuration;
            else if (requestedDuration is TimeSpan duration)
                return duration;

            return defaultDuration ?? DefaultLeaseDuration ?? MaxLeaseDuration;
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
