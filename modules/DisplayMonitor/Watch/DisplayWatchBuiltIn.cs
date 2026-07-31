using MadWizard.Desomnia.Display.Configuration;
using MadWizard.Desomnia.Display.Manager;
using MadWizard.Desomnia.Events;

namespace MadWizard.Desomnia.Display.Watch
{
    /// <summary>
    /// Watches the built-in panel — the physical home of the lid, and the only watch type
    /// that natively declares the lid events. Unlike external displays it has no
    /// connect/disconnect lifecycle (the panel is always there), and it creates no demand
    /// unless explicitly configured (see <see cref="DisplayBuiltInDescriptor.PreventIdle"/>).
    /// </summary>
    public class DisplayWatchBuiltIn : DisplayWatch
    {
        [EventContext]
        public override IDisplayBuiltIn Display { get; }

        [EventOpposite(nameof(LidClose))]
        public event EventInvocation? LidOpen;
        public event EventInvocation? LidClose;

        public DisplayWatchBuiltIn(IDisplayBuiltIn display, DisplayBuiltInDescriptor desc)
        {
            Display = display;

            ShouldPreventIdle = desc.PreventIdle;
            ShouldBeDisabled = desc.Disabled;

            LidOpen.AddAction(desc.OnLidOpen);
            LidClose.AddAction(desc.OnLidClose);
        }

        #region LidState tracking
        protected override void StartTrackingBy(ResourceMonitor monitor, bool adopt)
        {
            base.StartTrackingBy(monitor, adopt);

            Display.LidStateChanged += Display_LidStateChanged;
        }

        private void Display_LidStateChanged(object? sender, bool open)
        {
            (open ? LidOpen : LidClose).TriggerEvent();
        }

        protected override void StopTrackingBy(ResourceMonitor monitor)
        {
            // same seam the old monitor-relay unsubscribe covered in StopAsync: once the
            // monitor lets go, the lid must not fire actions into a stopping application
            Display.LidStateChanged -= Display_LidStateChanged;

            base.StopTrackingBy(monitor);
        }
        #endregion

        public override void Dispose()
        {
            Display.LidStateChanged -= Display_LidStateChanged;

            base.Dispose();
        }

        public override string ToString() => $"DisplayBuiltInWatch[{Display.Identity}]";
    }
}
