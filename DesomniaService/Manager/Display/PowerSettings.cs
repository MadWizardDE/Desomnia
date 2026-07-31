using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Display.Manager
{
    /// <summary>
    /// Callback-based power setting and suspend/resume notifications (powrprof) — no window and
    /// no service status handle required, so they work identically in session 0 and on the
    /// command line. A power setting's current value is delivered immediately upon registration.
    /// </summary>
    internal static class PowerSettings
    {
        /// <summary>Lid state: 0 = closed, 1 = open. Never fires on machines without a lid.</summary>
        public static readonly Guid LidSwitchStateChange = new("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");

        /// <summary>Console display state: 0 = off, 1 = on, 2 = dimmed.</summary>
        public static readonly Guid ConsoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");

        const uint DEVICE_NOTIFY_CALLBACK = 2;
        const uint PBT_POWERSETTINGCHANGE = 0x8013;

        // the only resume message sent for *every* wake; PBT_APMRESUMESUSPEND follows it
        // solely when the machine was woken by user activity
        const uint PBT_APMRESUMEAUTOMATIC = 0x0012;

        delegate uint DeviceNotifyCallbackRoutine(nint context, uint type, nint setting);

        [StructLayout(LayoutKind.Sequential)]
        struct DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
        {
            public DeviceNotifyCallbackRoutine Callback;
            public nint Context;
        }

        [DllImport("powrprof.dll")]
        static extern uint PowerSettingRegisterNotification(in Guid settingGuid, uint flags, in DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient, out nint registrationHandle);

        [DllImport("powrprof.dll")]
        static extern uint PowerSettingUnregisterNotification(nint registrationHandle);

        [DllImport("powrprof.dll")]
        static extern uint PowerRegisterSuspendResumeNotification(uint flags, in DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient, out nint registrationHandle);

        [DllImport("powrprof.dll")]
        static extern uint PowerUnregisterSuspendResumeNotification(nint registrationHandle);

        // keep the marshaled delegates alive for the lifetime of their registrations
        static readonly Dictionary<nint, DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS> _subscriptions = [];

        public static nint Register(Guid setting, Action<uint> handler)
        {
            var parameters = new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
            {
                Callback = (context, type, data) =>
                {
                    if (type == PBT_POWERSETTINGCHANGE && data != 0)
                    {
                        // POWERBROADCAST_SETTING: Guid(16) + DataLength(4) + Data
                        handler((uint)Marshal.ReadInt32(data + 20));
                    }

                    return 0;
                },
            };

            uint rc = PowerSettingRegisterNotification(in setting, DEVICE_NOTIFY_CALLBACK, in parameters, out nint handle);
            if (rc != 0)
                throw new Exception($"PowerSettingRegisterNotification({setting}) failed (rc={rc})");

            lock (_subscriptions)
                _subscriptions[handle] = parameters;

            return handle;
        }

        public static void Unregister(nint handle)
        {
            PowerSettingUnregisterNotification(handle);

            lock (_subscriptions)
                _subscriptions.Remove(handle);
        }

        /// <summary>Invokes <paramref name="handler"/> after every wake-up from sleep or hibernation.</summary>
        public static nint RegisterResume(Action handler)
        {
            var parameters = new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
            {
                Callback = (context, type, data) =>
                {
                    if (type == PBT_APMRESUMEAUTOMATIC)
                    {
                        handler();
                    }

                    return 0;
                },
            };

            uint rc = PowerRegisterSuspendResumeNotification(DEVICE_NOTIFY_CALLBACK, in parameters, out nint handle);
            if (rc != 0)
                throw new Exception($"PowerRegisterSuspendResumeNotification failed (rc={rc})");

            lock (_subscriptions)
                _subscriptions[handle] = parameters;

            return handle;
        }

        public static void UnregisterResume(nint handle)
        {
            PowerUnregisterSuspendResumeNotification(handle);

            lock (_subscriptions)
                _subscriptions.Remove(handle);
        }
    }
}
