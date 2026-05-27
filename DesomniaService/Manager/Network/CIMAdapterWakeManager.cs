using Microsoft.Extensions.Logging;
using Microsoft.Management.Infrastructure;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Manager
{
    internal class CIMAdapterWakeManager : IWakeOnLANManager
    {
        public required ILogger<CIMAdapterWakeManager> Logger { private get; init; }

        public required NetworkDevice Device { private get; init; }

        WakeOnLANMode IWakeOnLANManager.SupportedModes
        {
            get
            {
                return FindInstance("MSPower_DeviceWakeEnable") is not null
                    ? CompositeWakeOnLANManager.Pattern | WakeOnLANMode.MagicPacket
                    : WakeOnLANMode.None;
            }
        }

        WakeOnLANMode IWakeOnLANManager.Modes
        {
            get
            {
                if (!ReadProperty<bool>("MSPower_DeviceWakeEnable", "Enable"))
                    return WakeOnLANMode.None;

                if (ReadProperty<bool>("MSNdis_DeviceWakeOnMagicPacketOnly", "EnableWakeOnMagicPacketOnly"))
                    return WakeOnLANMode.MagicPacket;

                return CompositeWakeOnLANManager.Pattern | WakeOnLANMode.MagicPacket;
            }

            set
            {
                bool wake = value != WakeOnLANMode.None;

                WriteProperty("MSPower_DeviceWakeEnable", "Enable", wake);

                var onlyOnMagicPacket = wake && ((value & (value - (int)WakeOnLANMode.MagicPacket)) == 0);

                WriteProperty("MSNdis_DeviceWakeOnMagicPacketOnly", "EnableWakeOnMagicPacketOnly", onlyOnMagicPacket);
            }
        }

        #region CIM access
        private const string WMINamespace = @"root\wmi";
        private const string AdapterNamespace = @"root\StandardCimv2";

        private CimSession Session
        {
            get
            {
                if (field == null || !field.TestConnection())
                {
                    field?.Dispose();
                    field = null;

                    field = CimSession.Create(null);
                }

                return field;
            }

            set
            {
                if (value == null)
                {
                    field?.Dispose();
                    field = value;
                }
            }
        }

        private string PnPDeviceID
        {
            get
            {
                if (field == null)
                {
                    foreach (var instance in Session.EnumerateInstances(AdapterNamespace, "MSFT_NetAdapter"))
                        if (instance.CimInstanceProperties["NetworkAddresses"].Value is string[] addresses
                            && addresses.Any(a => string.Equals(a, $"{Device.PhysicalAddress}", StringComparison.OrdinalIgnoreCase)))
                        {
                            if ((bool?)instance.CimInstanceProperties["HadwareInterface"]?.Value ?? false)
                            {
                                return field = (string)instance.CimInstanceProperties["PnpDeviceID"].Value;
                            }
                        }

                    throw new InvalidOperationException($"No network adapter with MAC '{Device.PhysicalAddress.ToHexString()}' found in MSFT_NetAdapter.");
                }

                return field;
            }
        }

        private CimInstance? FindInstance(string className)
        {
            foreach (var instance in Session.EnumerateInstances(WMINamespace, className))
                if ((string)instance.CimInstanceProperties["InstanceName"].Value == PnPDeviceID)
                    return instance;

            return null;
        }
        private T? ReadProperty<T>(string className, string propertyName)
        {
            if (FindInstance(className) is CimInstance instance)
                return (T?)instance.CimInstanceProperties[propertyName].Value;

            return default;
        }
        private void WriteProperty<T>(string className, string propertyName, T value)
        {
            var instance = FindInstance(className) ?? throw new InvalidOperationException($"{className} not found");

            instance.CimInstanceProperties[propertyName].Value = value;
            Session.ModifyInstance(WMINamespace, instance);

            Logger.LogTrace("{className}.{propertyName} = {MagicPacketOnly}", className, propertyName, value);
        }
        #endregion
    }
}