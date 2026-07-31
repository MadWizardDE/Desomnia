using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace MadWizard.Desomnia.Display.Manager
{
    /// <summary>
    /// CfgMgr32 access to the PnP monitor devices — the only display enumeration that is
    /// reliable from session 0 (GDI is blind there, QueryDisplayConfig is access-denied).
    /// Validated against real hardware by probes/DisplayProbe.Windows.
    /// </summary>
    internal static partial class CfgMgr32
    {
        public static readonly Guid GUID_DEVINTERFACE_MONITOR = new("E6F07B5F-EE97-4A90-B076-33F57BF4EAA7");

        const uint CR_SUCCESS = 0;
        const uint CM_GET_DEVICE_INTERFACE_LIST_PRESENT = 0;
        const uint CM_LOCATE_DEVNODE_NORMAL = 0;

        const uint DEVPROP_TYPE_STRING = 0x12;

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVPROPKEY(string fmtid, uint pid)
        {
            public Guid fmtid = new(fmtid);
            public uint pid = pid;
        }

        public static readonly DEVPROPKEY DEVPKEY_Device_EnumeratorName = new("a45c254e-df1c-4efd-8020-67d146a850e0", 24);
        public static readonly DEVPROPKEY DEVPKEY_Device_InstanceId = new("78c34fc8-104a-4aca-9ea4-524d52996e57", 256);

        #region P/Invoke
        [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint CM_Get_Device_Interface_List_SizeW(out uint size, in Guid interfaceClassGuid, string? deviceId, uint flags);

        [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint CM_Get_Device_Interface_ListW(in Guid interfaceClassGuid, string? deviceId, [Out] char[] buffer, uint bufferLength, uint flags);

        [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint CM_Get_Device_Interface_PropertyW(string deviceInterface, in DEVPROPKEY propertyKey, out uint propertyType, [Out] byte[]? buffer, ref uint bufferSize, uint flags);

        [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

        [LibraryImport("cfgmgr32.dll")]
        private static partial uint CM_Get_DevNode_PropertyW(uint devInst, in DEVPROPKEY propertyKey, out uint propertyType, [Out] byte[]? buffer, ref uint bufferSize, uint flags);

        [LibraryImport("cfgmgr32.dll")]
        private static partial uint CM_Get_Parent(out uint parentDevInst, uint devInst, uint flags);

        [LibraryImport("cfgmgr32.dll")]
        private static partial uint CM_Open_DevNode_Key(uint devInst, uint samDesired, uint hardwareProfile, uint disposition, out nint hKey, uint flags);
        #endregion

        /// <summary>Lists the symbolic links of all interfaces of the given class that are currently present (= connected).</summary>
        public static string[] GetPresentInterfaces(Guid interfaceClass)
        {
            uint cr = CM_Get_Device_Interface_List_SizeW(out uint size, in interfaceClass, null, CM_GET_DEVICE_INTERFACE_LIST_PRESENT);
            if (cr != CR_SUCCESS)
                throw new Exception($"CM_Get_Device_Interface_List_SizeW failed (CR=0x{cr:X})");

            var buffer = new char[size];

            cr = CM_Get_Device_Interface_ListW(in interfaceClass, null, buffer, size, CM_GET_DEVICE_INTERFACE_LIST_PRESENT);
            if (cr != CR_SUCCESS)
                throw new Exception($"CM_Get_Device_Interface_ListW failed (CR=0x{cr:X})");

            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }

        public static string? GetInterfaceInstanceId(string symbolicLink)
        {
            uint size = 0;

            CM_Get_Device_Interface_PropertyW(symbolicLink, in DEVPKEY_Device_InstanceId, out _, null, ref size, 0);
            if (size == 0)
                return null;

            var buffer = new byte[size];

            if (CM_Get_Device_Interface_PropertyW(symbolicLink, in DEVPKEY_Device_InstanceId, out uint type, buffer, ref size, 0) != CR_SUCCESS)
                return null;

            return DecodeStringProperty(buffer, type);
        }

        public static uint LocateDevNode(string instanceId)
        {
            uint cr = CM_Locate_DevNodeW(out uint devInst, instanceId, CM_LOCATE_DEVNODE_NORMAL);
            if (cr != CR_SUCCESS)
                throw new Exception($"CM_Locate_DevNodeW({instanceId}) failed (CR=0x{cr:X})");

            return devInst;
        }

        public static string? GetDevNodeProperty(uint devInst, in DEVPROPKEY key)
        {
            uint size = 0;

            CM_Get_DevNode_PropertyW(devInst, in key, out _, null, ref size, 0);
            if (size == 0)
                return null;

            var buffer = new byte[size];

            if (CM_Get_DevNode_PropertyW(devInst, in key, out uint type, buffer, ref size, 0) != CR_SUCCESS)
                return null;

            return DecodeStringProperty(buffer, type);
        }

        private static string? DecodeStringProperty(byte[] buffer, uint type)
        {
            if (type != DEVPROP_TYPE_STRING)
                return null;

            return System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }

        /// <summary>Enumerator of the parent devnode (the display adapter): "PCI" = physical GPU, "SWD"/"ROOT" = virtual/indirect.</summary>
        public static string? GetParentEnumerator(uint devInst)
        {
            if (CM_Get_Parent(out uint parent, devInst, 0) != CR_SUCCESS)
                return null;

            return GetDevNodeProperty(parent, in DEVPKEY_Device_EnumeratorName);
        }

        /// <summary>Reads the raw EDID from the devnode's hardware key ("Device Parameters").</summary>
        public static byte[]? ReadEdid(uint devInst)
        {
            const uint KEY_READ = 0x20019;
            const uint RegDisposition_OpenExisting = 1;
            const uint CM_REGISTRY_HARDWARE = 0;

            if (CM_Open_DevNode_Key(devInst, KEY_READ, 0, RegDisposition_OpenExisting, out nint hKey, CM_REGISTRY_HARDWARE) != CR_SUCCESS)
                return null;

            using var key = RegistryKey.FromHandle(new SafeRegistryHandle(hKey, ownsHandle: true));

            return key.GetValue("EDID") as byte[];
        }

        #region device interface arrival/removal notification
        public const int CM_NOTIFY_ACTION_DEVICEINTERFACEARRIVAL = 0;
        public const int CM_NOTIFY_ACTION_DEVICEINTERFACEREMOVAL = 1;

        public delegate uint NotifyCallback(nint hNotify, nint context, int action, nint eventData, uint eventDataSize);

        [StructLayout(LayoutKind.Explicit, Size = 416)]
        private struct CM_NOTIFY_FILTER
        {
            [FieldOffset(0)] public uint cbSize;
            [FieldOffset(4)] public uint Flags;
            [FieldOffset(8)] public int FilterType; // 0 = CM_NOTIFY_FILTER_TYPE_DEVICEINTERFACE
            [FieldOffset(12)] public uint Reserved;
            [FieldOffset(16)] public Guid ClassGuid;
        }

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Register_Notification(in CM_NOTIFY_FILTER filter, nint context, NotifyCallback callback, out nint hNotify);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Unregister_Notification(nint hNotify);

        // keep the marshaled delegates alive for the lifetime of their registrations
        private static readonly Dictionary<nint, NotifyCallback> _callbacks = [];

        public static nint RegisterInterfaceNotification(Guid interfaceClass, NotifyCallback callback)
        {
            var filter = new CM_NOTIFY_FILTER
            {
                cbSize = 416,
                FilterType = 0, // device interface
                ClassGuid = interfaceClass,
            };

            uint cr = CM_Register_Notification(in filter, 0, callback, out nint hNotify);
            if (cr != CR_SUCCESS)
                throw new Exception($"CM_Register_Notification failed (CR=0x{cr:X})");

            lock (_callbacks)
                _callbacks[hNotify] = callback;

            return hNotify;
        }

        public static void UnregisterNotification(nint hNotify)
        {
            CM_Unregister_Notification(hNotify);

            lock (_callbacks)
                _callbacks.Remove(hNotify);
        }

        /// <summary>Extracts the symbolic link from a CM_NOTIFY_EVENT_DATA of filter type "device interface".</summary>
        public static string? GetEventSymbolicLink(nint eventData)
        {
            // CM_NOTIFY_EVENT_DATA: FilterType(4) + Reserved(4) + ClassGuid(16) + SymbolicLink[]
            return Marshal.PtrToStringUni(eventData + 24);
        }
        #endregion
    }
}
