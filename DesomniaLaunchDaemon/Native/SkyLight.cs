using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.LaunchDaemon.Native
{
    /// <summary>
    /// Private SkyLight display-configuration bindings for the BetterDisplay-style soft
    /// disconnect (probe-validated on macOS 15 by probes/DisplayProbe.MacOS, including from
    /// a root SSH shell — WindowServer accepts the connection without a GUI session):
    ///
    ///   SLSBeginDisplayConfiguration(&amp;config)
    ///   SLSConfigureDisplayEnabled(config, cgDisplayID, enabled)
    ///   SLSCompleteDisplayConfigurationWithOption(config, scope)
    ///
    /// WindowServer detaches the display from the desktop and drops the DCP link — the
    /// monitor sleeps, but its IOKit nodes survive; the outcome loops back to the manager
    /// as a DCP link-state message (0xE0115006), not as a disconnect.
    ///
    /// The connection must exist BEFORE any of the calls above: CGS hard-asserts the process
    /// out ("CGS_REQUIRE_INIT ... Abort trap: 6") when a configuration call arrives cold.
    /// The probe never hit this because it ran SLSMainConnectionID() first — which is what
    /// <see cref="EnsureConnection"/> now does, turning an unreachable WindowServer into a
    /// managed exception instead of a native abort.
    ///
    /// All changes are app-scoped (kCGConfigureForAppOnly) on purpose: WindowServer reverts
    /// them automatically when the daemon exits, so a crash can never leave displays dark.
    ///
    /// Symbols resolve at runtime (SLS* first, CGS* legacy fallback) because the names are
    /// private and have shifted between releases; likewise CGDisplayGetDisplayIDFromUUID,
    /// whose export moved from CoreGraphics to ColorSync on modern macOS. Missing symbols
    /// surface as NotSupportedException on first use, never at startup.
    ///
    /// Every native call is traced to the optional logger — argument line before, result
    /// line after, each flushed to disk immediately: should a call abort the process, the
    /// last line names the call and its parameters.
    /// </summary>
    public static unsafe class SkyLight
    {
        const string SkyLightFramework = "/System/Library/PrivateFrameworks/SkyLight.framework/SkyLight";
        const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        const string ColorSync = "/System/Library/Frameworks/ColorSync.framework/ColorSync";

        const int kCGConfigureForAppOnly = 0;

        static readonly nint _mainConnection, _begin, _configure, _complete, _cancel, _uuidToId, _createUuid, _onlineList, _vendorNumber, _modelNumber, _serialNumber, _isBuiltin;
        static readonly List<string> _resolved = [];
        static readonly List<string> _missing = [];

        static bool _connected;

        static SkyLight()
        {
            NativeLibrary.TryLoad(SkyLightFramework, out nint skyLight);
            NativeLibrary.TryLoad(CoreGraphics, out nint coreGraphics);
            NativeLibrary.TryLoad(ColorSync, out nint colorSync);

            nint Resolve(params string[] names)
            {
                foreach ((nint library, string source) in ((nint, string)[])[(skyLight, "SkyLight"), (coreGraphics, "CoreGraphics"), (colorSync, "ColorSync")])
                    if (library != 0)
                        foreach (string name in names)
                            if (NativeLibrary.TryGetExport(library, name, out nint export))
                            {
                                _resolved.Add($"{name} ({source})");

                                return export;
                            }

                _missing.Add(names[0]);

                return 0;
            }

            _mainConnection = Resolve("SLSMainConnectionID", "CGSMainConnectionID");
            _begin = Resolve("SLSBeginDisplayConfiguration", "CGSBeginDisplayConfiguration");
            _configure = Resolve("SLSConfigureDisplayEnabled", "CGSConfigureDisplayEnabled");
            _complete = Resolve("SLSCompleteDisplayConfigurationWithOption", "CGSCompleteDisplayConfigurationWithOption");
            _cancel = Resolve("SLSCancelDisplayConfiguration", "CGSCancelDisplayConfiguration");
            _uuidToId = Resolve("CGDisplayGetDisplayIDFromUUID");
            _createUuid = Resolve("CGDisplayCreateUUIDFromDisplayID");
            _onlineList = Resolve("CGGetOnlineDisplayList");
            _vendorNumber = Resolve("CGDisplayVendorNumber");
            _modelNumber = Resolve("CGDisplayModelNumber");
            _serialNumber = Resolve("CGDisplaySerialNumber");
            _isBuiltin = Resolve("CGDisplayIsBuiltin");
        }

        static string SymbolReport => string.Join(", ", _resolved) + (_missing.Count > 0 ? $"; MISSING: {string.Join(", ", _missing)}" : "");

        /// <summary>Trace that survives a native abort: each line is flushed to the targets
        /// before the next native call gets the chance to take the process down.</summary>
        static void Trace(ILogger? log, string message)
        {
            if (log == null || !log.IsEnabled(LogLevel.Trace))
                return;

            log.LogTrace(message);

            NLog.LogManager.Flush();
        }

        /// <summary>One-time CGS initialization: without it, the first configuration call
        /// aborts the whole process (CGS_REQUIRE_INIT). Throws instead when WindowServer
        /// cannot be reached from this context.</summary>
        static void EnsureConnection(ILogger? log)
        {
            if (_connected)
                return;

            Trace(log, $"Symbols: {SymbolReport}");

            if (_mainConnection == 0)
                throw new NotSupportedException($"SLSMainConnectionID unavailable — cannot establish a WindowServer connection. Missing: {string.Join(", ", _missing)}");

            Trace(log, "SLSMainConnectionID()...");

            int connection = ((delegate* unmanaged<int>)_mainConnection)();

            Trace(log, $"SLSMainConnectionID() = 0x{connection:X}");

            if (connection == 0)
                throw new InvalidOperationException("No WindowServer connection — display configuration is unavailable in this context.");

            _connected = true;
        }

        /// <summary>Resolves a CG display id from its EDID-derived UUID string.
        /// Returns 0 when unknown — note that soft-disconnected displays drop off this lookup.
        /// The reverse lookup (CGDisplayGetDisplayIDFromUUID) has been observed returning 0
        /// for perfectly online displays in the daemon context (macOS 15.5, 2026-07-23), so
        /// the online display list is scanned as a fallback — the direction the probe
        /// actually validated: enumerate ids, derive each display's UUID, match.</summary>
        public static uint DisplayIdFromUuid(string uuid, ILogger? log = null)
        {
            EnsureConnection(log);

            uint id = ReverseLookup(uuid, log);

            if (id == 0)
                id = ScanOnlineDisplays(uuid, log);

            return id;
        }

        static uint ReverseLookup(string uuid, ILogger? log)
        {
            if (_uuidToId == 0)
                return 0;

            nint cfString = CF.CreateString(uuid);

            try
            {
                nint cfUuid = CF.CFUUIDCreateFromString(0, cfString);

                if (cfUuid == 0)
                    return 0;

                try
                {
                    Trace(log, $"CGDisplayGetDisplayIDFromUUID(\"{uuid}\")...");

                    uint id = ((delegate* unmanaged<nint, uint>)_uuidToId)(cfUuid);

                    Trace(log, $"CGDisplayGetDisplayIDFromUUID(\"{uuid}\") = {id}");

                    return id;
                }
                finally
                {
                    CF.CFRelease(cfUuid);
                }
            }
            finally
            {
                CF.CFRelease(cfString);
            }
        }

        static uint ScanOnlineDisplays(string uuid, ILogger? log)
        {
            if (_onlineList == 0 || _createUuid == 0)
                return 0;

            uint* displays = stackalloc uint[16];
            uint count = 0;

            Trace(log, "CGGetOnlineDisplayList(16)...");

            int rc = ((delegate* unmanaged<uint, uint*, uint*, int>)_onlineList)(16, displays, &count);

            Trace(log, $"CGGetOnlineDisplayList(16) = {rc}, count = {count}");

            if (rc != 0)
                return 0;

            for (uint i = 0; i < count; i++)
            {
                uint display = displays[i];

                Trace(log, $"CGDisplayCreateUUIDFromDisplayID({display})...");

                nint cfUuid = ((delegate* unmanaged<uint, nint>)_createUuid)(display);

                if (cfUuid == 0)
                {
                    Trace(log, $"CGDisplayCreateUUIDFromDisplayID({display}) = <null>");

                    continue;
                }

                try
                {
                    nint cfString = CF.CFUUIDCreateString(0, cfUuid);

                    if (cfString == 0)
                        continue;

                    try
                    {
                        string? candidate = CF.ToManagedString(cfString);

                        Trace(log, $"CGDisplayCreateUUIDFromDisplayID({display}) = {candidate ?? "?"}");

                        if (string.Equals(candidate, uuid, StringComparison.OrdinalIgnoreCase))
                            return display;
                    }
                    finally
                    {
                        CF.CFRelease(cfString);
                    }
                }
                finally
                {
                    CF.CFRelease(cfUuid);
                }
            }

            return 0;
        }

        /// <summary>One online display as WindowServer reports it: the numeric id plus the
        /// EDID-sourced vendor/model/serial numbers (CGDisplayVendorNumber &amp; friends).</summary>
        public readonly record struct OnlineDisplay(uint Id, uint Vendor, uint Model, uint Serial);

        /// <summary>Enumerates the online displays with their EDID-sourced attributes — the
        /// identity bridge that still works when the UUID route fails: in the daemon context
        /// WindowServer has been observed handing out RANDOM v4 UUIDs instead of EDID-derived
        /// ones (macOS 15.5, 2026-07-23), while vendor/model/serial remain true EDID data.
        /// Empty on any failure.</summary>
        public static OnlineDisplay[] GetOnlineDisplays(ILogger? log = null)
        {
            if (_onlineList == 0 || _vendorNumber == 0 || _modelNumber == 0)
                return [];

            EnsureConnection(log);

            uint* displays = stackalloc uint[16];
            uint count = 0;

            Trace(log, "CGGetOnlineDisplayList(16)...");

            int rc = ((delegate* unmanaged<uint, uint*, uint*, int>)_onlineList)(16, displays, &count);

            Trace(log, $"CGGetOnlineDisplayList(16) = {rc}, count = {count}");

            if (rc != 0)
                return [];

            var online = new OnlineDisplay[count];

            for (uint i = 0; i < count; i++)
            {
                uint display = displays[i];

                Trace(log, $"CGDisplayVendorNumber/ModelNumber/SerialNumber({display})...");

                uint vendor = ((delegate* unmanaged<uint, uint>)_vendorNumber)(display);
                uint model = ((delegate* unmanaged<uint, uint>)_modelNumber)(display);
                uint serial = _serialNumber != 0 ? ((delegate* unmanaged<uint, uint>)_serialNumber)(display) : 0;

                Trace(log, $"CGDisplayVendorNumber/ModelNumber/SerialNumber({display}) = 0x{vendor:X4}, 0x{model:X4}, {serial}");

                online[i] = new OnlineDisplay(display, vendor, model, serial);
            }

            return online;
        }

        /// <summary>Finds the built-in panel among the online displays (CGDisplayIsBuiltin) —
        /// the only identity bridge the panel has: its pipe carries no EDID UUID and its
        /// ManufacturerID is an OUI rather than a PnP code, so neither external route can name
        /// it. Returns 0 when it is not online — a soft-disconnected panel drops off the list,
        /// which is why the resolved id is cached while the panel is still driven.</summary>
        public static uint GetBuiltInDisplayId(ILogger? log = null)
        {
            if (_onlineList == 0 || _isBuiltin == 0)
                return 0;

            EnsureConnection(log);

            uint* displays = stackalloc uint[16];
            uint count = 0;

            Trace(log, "CGGetOnlineDisplayList(16)...");

            int rc = ((delegate* unmanaged<uint, uint*, uint*, int>)_onlineList)(16, displays, &count);

            Trace(log, $"CGGetOnlineDisplayList(16) = {rc}, count = {count}");

            if (rc != 0)
                return 0;

            for (uint i = 0; i < count; i++)
            {
                uint display = displays[i];

                Trace(log, $"CGDisplayIsBuiltin({display})...");

                int builtin = ((delegate* unmanaged<uint, int>)_isBuiltin)(display);

                Trace(log, $"CGDisplayIsBuiltin({display}) = {builtin}");

                if (builtin != 0)
                    return display;
            }

            return 0;
        }

        /// <summary>Soft-connects/disconnects a display at the WindowServer level (app-scoped).</summary>
        public static void SetDisplayEnabled(uint display, bool enabled, ILogger? log = null)
        {
            if (_begin == 0 || _configure == 0 || _complete == 0)
                throw new NotSupportedException($"SkyLight display configuration unavailable, missing: {string.Join(", ", _missing)}");

            EnsureConnection(log);

            nint config = 0;

            Trace(log, "SLSBeginDisplayConfiguration()...");

            int rc = ((delegate* unmanaged<nint*, int>)_begin)(&config);

            Trace(log, $"SLSBeginDisplayConfiguration() = {rc}, config = 0x{config:X}");

            if (rc != 0)
                throw new InvalidOperationException($"SLSBeginDisplayConfiguration failed: {DescribeError(rc)}");

            Trace(log, $"SLSConfigureDisplayEnabled(0x{config:X}, {display}, {(enabled ? 1 : 0)})...");

            rc = ((delegate* unmanaged<nint, uint, byte, int>)_configure)(config, display, enabled ? (byte)1 : (byte)0);

            Trace(log, $"SLSConfigureDisplayEnabled(0x{config:X}, {display}, {(enabled ? 1 : 0)}) = {rc}");

            if (rc != 0)
            {
                if (_cancel != 0)
                    ((delegate* unmanaged<nint, int>)_cancel)(config);

                throw new InvalidOperationException($"SLSConfigureDisplayEnabled({display}, {enabled}) failed: {DescribeError(rc)}");
            }

            Trace(log, $"SLSCompleteDisplayConfigurationWithOption(0x{config:X}, kCGConfigureForAppOnly)...");

            rc = ((delegate* unmanaged<nint, int, int>)_complete)(config, kCGConfigureForAppOnly);

            Trace(log, $"SLSCompleteDisplayConfigurationWithOption(0x{config:X}, kCGConfigureForAppOnly) = {rc}");

            if (rc != 0)
                throw new InvalidOperationException($"SLSCompleteDisplayConfigurationWithOption failed: {DescribeError(rc)}");
        }

        static string DescribeError(int error) => error switch
        {
            1000 => "kCGErrorFailure (1000)",
            1001 => "kCGErrorIllegalArgument (1001)",
            1002 => "kCGErrorInvalidConnection (1002) — no WindowServer connection?",
            1003 => "kCGErrorInvalidContext (1003)",
            1004 => "kCGErrorCannotComplete (1004)",
            1006 => "kCGErrorNotImplemented (1006)",
            1007 => "kCGErrorRangeCheck (1007)",
            1008 => "kCGErrorTypeCheck (1008)",
            1010 => "kCGErrorInvalidOperation (1010)",
            1011 => "kCGErrorNoneAvailable (1011)",
            _ => $"CGError {error}",
        };
    }
}
