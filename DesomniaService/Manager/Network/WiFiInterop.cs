using System.Runtime.InteropServices;
using System.Text;

namespace MadWizard.Desomnia.Network.Manager
{
    /// <summary>
    /// The slice of the Native Wifi API (wlanapi.dll) that answers the one question
    /// System.Net.NetworkInformation cannot: which wireless network an interface is joined to.
    ///
    /// Every entry point here is a query against the WLAN AutoConfig service (Wlansvc), which
    /// answers from session 0 just as well as from a desktop session. Machines without wireless
    /// hardware — Server SKUs in particular — do not run that service at all, which is not an
    /// error but simply an absence of any SSID to report.
    /// </summary>
    internal static partial class WiFiInterop
    {
        const string WLANAPI = "wlanapi.dll";

        const uint ERROR_SUCCESS = 0;

        /// <summary>Client version 2 negotiates the Vista-and-later API.</summary>
        const uint WLAN_CLIENT_VERSION = 2;

        /// <summary>WLAN_INTF_OPCODE.wlan_intf_opcode_current_connection.</summary>
        const uint WLAN_INTF_OPCODE_CURRENT_CONNECTION = 7;

        const int WLAN_MAX_NAME_LENGTH = 256;
        const int DOT11_SSID_MAX_LENGTH = 32;

        /// <summary>
        /// Reads the SSID the given interface is currently associated with.
        /// </summary>
        /// <param name="interfaceId">The adapter GUID, i.e. a parsed <see cref="System.Net.NetworkInformation.NetworkInterface.Id"/>.</param>
        /// <returns>
        /// The network name, or null when the interface is not a wireless one, is not associated,
        /// or the WLAN service is unavailable — every one of which simply means "joined to nothing".
        /// </returns>
        public static string? GetCurrentSSID(Guid interfaceId)
        {
            if (WlanOpenHandle(WLAN_CLIENT_VERSION, 0, out _, out nint client) != ERROR_SUCCESS)
                return null; // no WLAN AutoConfig service on this machine

            try
            {
                // fails with ERROR_INVALID_PARAMETER for an interface the service does not know
                // and with ERROR_INVALID_STATE while it is not connected to anything
                if (WlanQueryInterface(client, in interfaceId, WLAN_INTF_OPCODE_CURRENT_CONNECTION,
                        0, out _, out nint data, 0) != ERROR_SUCCESS)
                    return null;

                try
                {
                    return ReadSSID(data);
                }
                finally
                {
                    WlanFreeMemory(data);
                }
            }
            finally
            {
                WlanCloseHandle(client, 0);
            }
        }

        private static unsafe string? ReadSSID(nint data)
        {
            var connection = (WLAN_CONNECTION_ATTRIBUTES*)data;

            if (connection->isState != WLAN_INTERFACE_STATE.Connected)
                return null;

            // the length is what the driver reports; clamp it before trusting it as a buffer bound
            int length = (int)Math.Min(connection->dot11Ssid.uSSIDLength, DOT11_SSID_MAX_LENGTH);

            // 802.11 leaves an SSID an opaque byte string, but Windows and every modern access
            // point put UTF-8 in it
            return Encoding.UTF8.GetString(connection->dot11Ssid.ucSSID, length);
        }

        #region Windows-API
        [LibraryImport(WLANAPI)]
        private static partial uint WlanOpenHandle(uint dwClientVersion, nint pReserved,
            out uint pdwNegotiatedVersion, out nint phClientHandle);

        [LibraryImport(WLANAPI)]
        private static partial uint WlanCloseHandle(nint hClientHandle, nint pReserved);

        [LibraryImport(WLANAPI)]
        private static partial uint WlanQueryInterface(nint hClientHandle, in Guid pInterfaceGuid,
            uint OpCode, nint pReserved, out uint pdwDataSize, out nint ppData, nint pWlanOpcodeValueType);

        [LibraryImport(WLANAPI)]
        private static partial void WlanFreeMemory(nint pMemory);

        private enum WLAN_INTERFACE_STATE
        {
            NotReady = 0,
            Connected = 1,
            AdHocNetworkFormed = 2,
            Disconnecting = 3,
            Disconnected = 4,
            Associating = 5,
            Discovering = 6,
            Authenticating = 7,
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct WLAN_CONNECTION_ATTRIBUTES
        {
            public WLAN_INTERFACE_STATE isState;
            public int wlanConnectionMode; // WLAN_CONNECTION_MODE
            public fixed char strProfileName[WLAN_MAX_NAME_LENGTH];

            /// <summary>
            /// The head of the WLAN_ASSOCIATION_ATTRIBUTES that follows. The remainder of that
            /// structure, and the WLAN_SECURITY_ATTRIBUTES behind it, are deliberately left out —
            /// this is only ever read as a prefix of the buffer the service hands back.
            /// </summary>
            public DOT11_SSID dot11Ssid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct DOT11_SSID
        {
            public uint uSSIDLength;
            public fixed byte ucSSID[DOT11_SSID_MAX_LENGTH];
        }
        #endregion
    }
}
