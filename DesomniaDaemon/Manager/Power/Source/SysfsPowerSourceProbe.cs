namespace MadWizard.Desomnia.Power.Source
{
    /// <summary>
    /// Probe over <see cref="SysfsPowerSource"/>; changes are detected by polling
    /// (see <see cref="PollingPowerSource"/>).
    /// </summary>
    internal sealed class SysfsPowerSourceProbe : PollingPowerSource
    {
        public override PowerSource Source => SysfsPowerSource.Read();
    }
}
