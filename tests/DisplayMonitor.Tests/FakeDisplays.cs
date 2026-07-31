using MadWizard.Desomnia.Display.Manager;

namespace MadWizard.Desomnia.Display.Tests
{
    /// <summary>
    /// A built-in panel with a scriptable lid: <see cref="FlipLid"/> mirrors the platform
    /// contract of <see cref="IDisplayBuiltIn.LidStateChanged"/> (state updated, then one
    /// transition event). Subscription state is observable so tests can verify the lazy
    /// subscribe/unsubscribe behavior of consumers.
    /// </summary>
    internal sealed class FakeDisplayBuiltIn : IDisplayBuiltIn
    {
        public bool? LidOpen { get; set; }

        private EventHandler<bool>? _lidStateChanged;

        public event EventHandler<bool> LidStateChanged
        {
            add => _lidStateChanged += value;
            remove => _lidStateChanged -= value;
        }

        public int LidSubscriberCount => _lidStateChanged?.GetInvocationList().Length ?? 0;

        public void FlipLid(bool open)
        {
            LidOpen = open;

            _lidStateChanged?.Invoke(this, open);
        }

        public bool? IsOnline { get; set; } = true;

        public DisplayIdentity Identity { get; set; } = new() { VendorId = "APP", ProductCode = 0xA042, Name = "Fake Built-in Panel" };

        public Resolution? NativeResolution { get; set; }

        public bool ShouldBeDisabled { get; set; }

        public event EventHandler IsOnlineChanged { add { } remove { } }
    }

    internal sealed class FakeDisplayExternal : IDisplayExternal
    {
        public bool IsConnected { get; set; } = true;

        public DisplayConnection? Connection { get; set; } = DisplayConnection.DisplayPort;

        public bool? IsOnline { get; set; } = true;

        public DisplayIdentity Identity { get; set; } = new() { VendorId = "DEL", ProductCode = 0x4159, Name = "Fake External Display" };

        public Resolution? NativeResolution { get; set; } = new Resolution(3840, 2160);

        public bool ShouldBeDisabled { get; set; }

        public event EventHandler IsOnlineChanged { add { } remove { } }
    }

    internal sealed class FakeDisplayManager : IDisplayManager
    {
        public IDisplayBuiltIn? BuiltIn { get; set; }

        public List<IDisplay> Displays { get; } = [];

        public event EventHandler<IDisplayExternal>? DisplayConnected;
        public event EventHandler<IDisplayExternal>? DisplayDisconnected;

        public void RaiseConnected(IDisplayExternal display) => DisplayConnected?.Invoke(this, display);
        public void RaiseDisconnected(IDisplayExternal display) => DisplayDisconnected?.Invoke(this, display);

        public IEnumerator<IDisplay> GetEnumerator() => Displays.GetEnumerator();
    }
}
