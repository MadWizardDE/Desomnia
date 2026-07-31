namespace MadWizard.Desomnia.Display.Manager
{
    public interface IDisplayExternal : IDisplay
    {
        /// <summary>
        /// Whether the display is currently physically connected — the OS-level link is
        /// present (macOS: a live DCPAVServiceProxy). Independent of <see cref="IDisplay.IsOnline"/>:
        /// a soft-disconnected display stays physically connected (IsConnected == true) but
        /// is not being driven (IsOnline == false). A disconnected display is no longer
        /// enumerated by the <see cref="IDisplayManager"/>, but the instance stays valid for
        /// whoever still references it — and resurfaces as the same instance when the display
        /// reconnects (the manager's identity guarantee).
        /// </summary>
        bool IsConnected { get; }

        DisplayConnection? Connection { get; }
    }

    public enum DisplayConnection
    {
        Other,

        VGA,
        DVI,
        HDMI,
        DisplayPort,

        /// <summary>Built-in panel connection (eDP/LVDS/internal).</summary>
        Internal,
    }
}
