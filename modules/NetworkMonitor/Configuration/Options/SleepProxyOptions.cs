namespace MadWizard.Desomnia.Network.Configuration.Options
{
    public readonly struct SleepProxyOptions
    {
        public TimeSpan MinLeaseDuration { get; init; }
        public TimeSpan MaxLeaseDuration { get; init; }
    }
}
