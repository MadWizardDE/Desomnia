namespace MadWizard.Desomnia.Display.Manager
{
    internal abstract class WindowsDisplay : IDisplay
    {
        internal string SymbolicLink { get; private set; }
        internal string InstanceId { get; private set; }

        public DisplayIdentity Identity { get; }

        public Resolution? NativeResolution { get; private set; }

        private bool? _online;

        public bool? IsOnline => _online;

        public bool ShouldBeDisabled
        {
            get => false; // Windows cannot soft-disconnect a display, so we never hold one off
            set => throw new NotSupportedException("Soft connect/disconnect of displays is not supported on Windows.");
        }

        public event EventHandler? IsOnlineChanged;

        protected WindowsDisplay(string symbolicLink, string instanceId, EDID edid)
        {
            SymbolicLink = symbolicLink;
            InstanceId = instanceId;

            Identity = edid.ToIdentity();
            NativeResolution = edid.NativeResolution;
        }

        /// <summary>Points the recalled instance at the interface the display reconnected
        /// under — the identity is equal by definition, everything hardware-bound may differ.</summary>
        internal void Rebind(string symbolicLink, string instanceId, EDID edid)
        {
            SymbolicLink = symbolicLink;
            InstanceId = instanceId;

            NativeResolution = edid.NativeResolution;
        }

        internal void UpdateOnline(bool? state)
        {
            if (_online != state)
            {
                _online = state;

                IsOnlineChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public override string ToString() => Identity.ToString();
    }

    internal sealed class WindowsBuiltInDisplay(string symbolicLink, string instanceId, EDID edid)
        : WindowsDisplay(symbolicLink, instanceId, edid), IDisplayBuiltIn
    {
        private bool? _lidOpen;

        public bool? LidOpen => _lidOpen;

        public event EventHandler<bool>? LidStateChanged;

        internal void UpdateLidOpen(bool? open)
        {
            if (_lidOpen != open)
            {
                _lidOpen = open;

                if (open is bool value)
                    LidStateChanged?.Invoke(this, value);
            }
        }

        public override string ToString() => $"{Identity} [Internal]";
    }

    internal sealed class WindowsExternalDisplay(string symbolicLink, string instanceId, EDID edid, DisplayConnection? connection)
        : WindowsDisplay(symbolicLink, instanceId, edid), IDisplayExternal
    {
        public bool IsConnected { get; internal set; } = true;

        public DisplayConnection? Connection { get; private set; } = connection;

        internal void Rebind(string symbolicLink, string instanceId, EDID edid, DisplayConnection? connection)
        {
            Rebind(symbolicLink, instanceId, edid);

            Connection = connection;

            IsConnected = true;
        }

        public override string ToString() => $"{Identity} [{Connection?.ToString() ?? "?"}]";
    }
}
