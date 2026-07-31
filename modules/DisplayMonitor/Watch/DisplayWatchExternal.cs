using MadWizard.Desomnia.Display.Configuration;
using MadWizard.Desomnia.Display.Manager;
using MadWizard.Desomnia.Events;

namespace MadWizard.Desomnia.Display.Watch
{
    /// <summary>
    /// Watches one physically connected external display. The watch lifecycle IS the
    /// (debounced) connection state: it starts tracking on connect and stops on debounced
    /// disconnect; the raw per-instant state is <see cref="IDisplayExternal.IsConnected"/>.
    /// The manager's identity guarantee keeps <see cref="Display"/> a stable reference —
    /// a display reappearing within the debounce window is the same instance, and the
    /// watch simply continues. Whether being tracked counts as system demand is governed
    /// by the effective <see cref="PreventIdleType"/> mode.
    /// </summary>
    public class DisplayWatchExternal : DisplayWatch
    {
        [EventContext]
        public override IDisplayExternal Display { get; }

        [EventOpposite(nameof(Disconnect))]
        public event EventInvocation? Connect;
        public event EventInvocation? Disconnect;

        [EventOpposite(nameof(PowerOff))]
        public event EventInvocation? PowerOn;
        public event EventInvocation? PowerOff;

        public DisplayWatchExternal(IDisplayExternal display)
        {
            Display = display;

            display.IsOnlineChanged += Display_PowerChanged;
        }

        private void Display_PowerChanged(object? sender, EventArgs e)
        {
            switch (Display.IsOnline)
            {
                case true:
                    PowerOn.TriggerEvent();
                    break;
                case false:
                    PowerOff.TriggerEvent();
                    break;
            }
        }

        internal void ApplyConfiguration(DisplayMonitorConfig config, DisplayWatchDescriptor? desc)
        {
            ShouldPreventIdle = desc?.PreventIdle ?? config.PreventIdle;
            ShouldBeDisabled = desc?.Disabled ?? config.Disabled;

            if (desc is not null)
            {
                Connect.AddAction(desc.OnConnect);
                Disconnect.AddAction(desc.OnDisconnect);
                PowerOn.AddAction(desc.OnPowerOn);
                PowerOff.AddAction(desc.OnPowerOff);
            }
        }

        internal void TriggerConnect() => Connect.TriggerEvent();
        internal void TriggerDisconnect() => Disconnect.TriggerEvent();

        public override void Dispose()
        {
            Display.IsOnlineChanged -= Display_PowerChanged;

            base.Dispose();
        }

        public override string ToString() => $"DisplayWatch[{Display.Identity}]";
    }
}
