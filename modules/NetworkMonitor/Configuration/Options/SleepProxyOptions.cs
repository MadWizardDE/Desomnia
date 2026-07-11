namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct SleepProxyOptions
    {
        public int          LeaseLimit              { get; init; }

        public TimeSpan     LeaseDurationMin        { get; init; }
        public TimeSpan     LeaseDurationMax        { get; init; }

        public LeaseExpireAction LeaseExpire        { get; init; }

        public TimeSpan ClampLeaseDuration(TimeSpan requestedDuration)
        {
            if (requestedDuration < LeaseDurationMin)
                return LeaseDurationMin;
            else if (requestedDuration > LeaseDurationMax)
                return LeaseDurationMax;
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
