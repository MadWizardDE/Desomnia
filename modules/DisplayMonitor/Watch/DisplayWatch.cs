using MadWizard.Desomnia.Display.Configuration;
using MadWizard.Desomnia.Display.Manager;

namespace MadWizard.Desomnia.Display.Watch
{
    /// <summary>
    /// Common roster type of the <see cref="DisplayMonitor"/>: external displays
    /// (<see cref="DisplayWatchExternal"/>) and the built-in panel (<see cref="DisplayWatchBuiltIn"/>).
    /// </summary>
    public abstract class DisplayWatch : Resource
    {
        public abstract IDisplay Display { get; }

        protected PreventIdleType ShouldPreventIdle { get; set; }

        /// <summary>
        /// The effective configured intent whether this display should be held
        /// soft-disconnected (see <see cref="IDisplay.ShouldBeDisabled"/>). The watch only
        /// carries the resolved configuration value — asserting it against the display is
        /// the <see cref="DisplayMonitor"/>'s job, in its desired-state sweep.
        /// </summary>
        internal bool ShouldBeDisabled { get; private protected set; }

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            switch (ShouldPreventIdle)
            {
                case PreventIdleType.Never:
                    yield break;

                case PreventIdleType.Always:
                case PreventIdleType.Enabled when Display.IsOnline != false:
                    yield return new DisplayUsage(Display); break;
            }
        }
    }
}
