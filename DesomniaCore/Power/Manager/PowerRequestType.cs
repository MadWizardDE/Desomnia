namespace MadWizard.Desomnia.Power.Manager
{
    /// <summary>
    /// What a power request keeps awake. Platform managers map each value to its native
    /// identity (Win32 POWER_REQUEST_TYPE, IOPM assertion type, logind inhibitor "what").
    /// </summary>
    public enum PowerRequestType
    {
        /// <summary>Keep the system from going to sleep.</summary>
        System,

        /// <summary>Keep the display from blanking / idle-sleeping.</summary>
        Display,
    }
}
