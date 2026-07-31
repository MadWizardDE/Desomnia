using MadWizard.Desomnia.Display.Configuration.Converter;
using System.ComponentModel;

namespace MadWizard.Desomnia.Display.Manager
{
    /// <summary>
    /// A physical display. While it is connected the manager enumerates it; on disconnect
    /// the manager raises <see cref="IDisplayManager.DisplayDisconnected"/> and stops
    /// enumerating it — but the instance stays valid, and as long as anyone still holds a
    /// reference to it, a reconnect of the same physical display resurfaces THE SAME
    /// instance (see <see cref="IDisplayManager"/>). Consumers therefore never need any
    /// identity check beyond reference equality.
    /// </summary>
    public interface IDisplay
    {
        /// <summary>
        /// Whether the display is actually being driven right now, where the platform can
        /// tell (DP link state, macOS DCP link, console display state). False when the panel
        /// is soft-disconnected — by us OR by another tool such as BetterDisplay. Null when
        /// unknowable, e.g. HDMI sinks that keep the link alive while switched off. This is
        /// purely observed reality; to soft-connect/disconnect a display, set
        /// <see cref="ShouldBeDisabled"/>. <see cref="IsOnlineChanged"/>
        /// fires whenever the observed state actually changes.
        /// </summary>
        bool? IsOnline { get; }

        DisplayIdentity Identity { get; }

        Resolution? NativeResolution { get; }

        /// <summary>
        /// Our OWN intention for this display: set true to soft-disconnect it, false to
        /// soft-reconnect it. This is a declared intent, not an immediate result — disabling
        /// is applied at once (the panel is present and drivable), while re-enabling a panel
        /// whose physical link is currently down (sleep, unplug) is applied the moment the
        /// link returns. Reading it reports only what WE want; the actual state is
        /// <see cref="IsOnline"/>, which also reflects soft-disconnects made by
        /// other tools. The manager will not fight a foreign soft-disconnect: setting this
        /// false does not force a display back on that an external tool is holding off.
        /// Throws NotSupportedException on platforms without soft disconnect.
        /// </summary>
        bool ShouldBeDisabled { get; set; }

        event EventHandler IsOnlineChanged;
    }

    /// <summary>Pixel dimensions; configurable as "3840x2160".</summary>
    [TypeConverter(typeof(ResolutionConverter))]
    public readonly record struct Resolution(int Width, int Height)
    {
        public override string ToString() => $"{Width}x{Height}";
    }
}
