namespace MadWizard.Desomnia.Display.Manager
{
    internal abstract class MacOSDisplay(MacOSDisplayManager manager, string pipe, DisplayIdentity identity, Resolution? nativeResolution, string? edidUuid) : IDisplay
    {
        /// <summary>The display pipe token this display is driven by, e.g. "disp0" or "dispext1".</summary>
        internal string Pipe { get; private set; } = pipe;

        /// <summary>The pipe's "EDID UUID" — EDID-derived and identical to the CoreGraphics
        /// display UUID. Absent on the built-in panel (no EDID UUID on disp0).</summary>
        internal string? EdidUuid { get; private set; } = edidUuid;

        /// <summary>Cached CGDirectDisplayID — stable per physical display across reconnect
        /// (WindowServer reuses it), so it is kept even while the display is physically
        /// disconnected; it just cannot take a SkyLight transaction until the DCP proxy
        /// returns. Resolved lazily, never wiped.</summary>
        internal uint CGDisplayId { get; set; }

        public DisplayIdentity Identity => identity;

        public Resolution? NativeResolution { get; private set; } = nativeResolution;

        /// <summary>Whether the display path is physically present right now — the gate for
        /// applying soft state (see <see cref="MacOSDisplayManager"/>'s Reconcile): external
        /// displays answer with their OS-level link (<see cref="IDisplayExternal.IsConnected"/>),
        /// the built-in panel with its embedded DCP proxy (its power). An absent path is never
        /// applied to; its returning proxy reconciles.</summary>
        internal abstract bool IsPresent { get; }

        /// <summary>Points the recalled instance at the pipe the display reconnected under —
        /// the identity is equal by definition, everything hardware-bound may differ.</summary>
        private protected void Rebind(string pipe, Resolution? nativeResolution, string? edidUuid)
        {
            Pipe = pipe;
            EdidUuid = edidUuid;

            NativeResolution = nativeResolution;

            // observed state belongs to the previous connection life — the returning DCP link
            // will report the fresh one; null lets the next message drive a real edge
            _online = null;
        }

        private bool? _online;

        /// <summary>Observed software state: whether the panel is actually being driven right
        /// now (false when soft-disconnected, by us OR another tool). Updated only from the
        /// DCP link-state messages and proxy lifecycle — never from our own intention.</summary>
        public bool? IsOnline => _online;

        public event EventHandler? IsOnlineChanged;

        /// <summary>Counts observed link episodes — bumped on every <see cref="IsOnline"/> edge.
        /// It lets a deferred check tell whether it is still looking at the very episode it was
        /// armed for, or at a later one that merely looks the same (see the manager's foreign-hold
        /// settling). Written under the manager's lock, like <see cref="IsOnline"/> itself.</summary>
        internal uint LinkEpisode { get; private set; }

        internal void UpdateOnline(bool? state)
        {
            if (_online != state)
            {
                _online = state;

                LinkEpisode++;

                IsOnlineChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool _shouldBeDisabled;

        public bool ShouldBeDisabled
        {
            get => _shouldBeDisabled;
            set => manager.SetShouldBeDisabled(this, value);
        }

        /// <summary>The manager sets our intention under its lock, then reconciles.</summary>
        internal void SetShouldBeDisabledIntent(bool value) => _shouldBeDisabled = value;

        /// <summary>Whether OUR soft-disable is currently in force at WindowServer. Drives
        /// reconciliation together with <see cref="ShouldBeDisabled"/> — NOT the observed
        /// <see cref="IsOnline"/>, which a returning disabled display never reports.
        /// It PERSISTS across a physical reconnect, because macOS remembers the app-scoped hold:
        /// a re-docked display comes back still disabled, so its enable must be re-applied.</summary>
        internal bool DisableApplied { get; set; }

        public override string ToString() => $"{Identity} [{Pipe}]";
    }

    internal sealed class MacOSBuiltInDisplay(MacOSDisplayManager manager, string pipe, DisplayIdentity identity, Resolution? nativeResolution, string? edidUuid)
        : MacOSDisplay(manager, pipe, identity, nativeResolution, edidUuid), IDisplayBuiltIn
    {
        /// <summary>Whether the embedded DCP proxy is live — the panel's analog of
        /// <see cref="IDisplayExternal.IsConnected"/>: the proxy IS the panel's power, it
        /// terminates when the panel goes dark (sleep / clamshell) and re-arrives on wake.
        /// Maintained by the manager under its lock, alongside its proxy table.</summary>
        internal bool EmbeddedProxyPresent { get; set; }

        internal override bool IsPresent => EmbeddedProxyPresent;

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

        public override string ToString() => $"{Identity} [Internal, {Pipe}]";
    }

    internal sealed class MacOSExternalDisplay(MacOSDisplayManager manager, string pipe, DisplayIdentity identity, Resolution? nativeResolution, string? edidUuid, DisplayConnection? connection)
        : MacOSDisplay(manager, pipe, identity, nativeResolution, edidUuid), IDisplayExternal
    {
        public bool IsConnected { get; internal set; } = true;

        internal override bool IsPresent => IsConnected;

        public DisplayConnection? Connection { get; private set; } = connection;

        internal void Rebind(string pipe, Resolution? nativeResolution, string? edidUuid, DisplayConnection? connection)
        {
            Rebind(pipe, nativeResolution, edidUuid);

            Connection = connection;

            IsConnected = true;

            // both the intention AND the committed hold are DELIBERATELY kept across a physical
            // reconnect — a re-docked display returns to the state we hold it in, and macOS
            // remembers the hold, so DisableApplied still reflects reality. Observed state
            // resets to null (base Rebind); a link-up message later re-syncs DisableApplied.
        }

        public override string ToString() => $"{Identity} [{Connection?.ToString() ?? "?"}, {Pipe}]";
    }
}
