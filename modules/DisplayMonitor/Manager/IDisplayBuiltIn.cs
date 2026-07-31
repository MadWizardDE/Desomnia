namespace MadWizard.Desomnia.Display.Manager
{
    public interface IDisplayBuiltIn : IDisplay
    {
        /// <summary>Lid state — meaningful on the built-in panel, Unknown elsewhere.</summary>
        bool? LidOpen { get; }

        /// <summary>
        /// Raised on lid transitions, and only on transitions — the platform is the authority
        /// on the lid: it never reports the same state twice, and it reconciles the state
        /// across sleep, where the switch notification is not delivered. A lid operated while
        /// the machine was suspended therefore reports its new state right after the wake-up.
        /// Consumers can act on every raise and never need to cache the previous state or
        /// watch the power manager themselves.
        /// </summary>
        event EventHandler<bool> LidStateChanged;
    }
}
