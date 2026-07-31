namespace MadWizard.Desomnia.Display.Manager
{
    /// <summary>
    /// Platform view of the physically connected displays.
    ///
    /// Enumeration yields only displays that are currently connected — a display that is
    /// unplugged disappears from the manager (after <see cref="DisplayDisconnected"/>).
    /// Virtual/remote displays (RDP indirect displays etc.) are never surfaced at all.
    ///
    /// Identity guarantee: the manager remembers a disconnected display for as long as
    /// anyone still references the instance (weakly — see <see cref="DisplayMemory{TDisplay}"/>),
    /// and a reconnect of the same physical display resurfaces THE SAME instance through
    /// <see cref="DisplayConnected"/>. Upper layers can rely on reference equality alone;
    /// only once the last reference is collected does a reconnect produce a new instance.
    /// </summary>
    public interface IDisplayManager : IIEnumerable<IDisplay>
    {
        /// <summary>The built-in panel (laptops), or null on headless/desktop machines.</summary>
        IDisplayBuiltIn? BuiltIn { get; }

        event EventHandler<IDisplayExternal> DisplayConnected;
        event EventHandler<IDisplayExternal> DisplayDisconnected;
    }
}
