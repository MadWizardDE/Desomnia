using Microsoft.Extensions.Logging;
using Microsoft.Management.Infrastructure;

namespace MadWizard.Desomnia.Network.Manager
{
    internal class CIMNetAdapterPowerManagement : CIMNetAdapterBase, IWakeOnLANManager
    {
        public required ILogger<CIMNetAdapterPowerManagement> Logger { private get; init; }

        WakeOnLANMode IWakeOnLANManager.SupportedModes
        {
            get
            {
                var result = WakeOnLANMode.None;
                if (this["WakeOnMagicPacket"] != null)
                    result |= WakeOnLANMode.MagicPacket;
                if (this["WakeOnPattern"] != null)
                    result |= Pattern;

                return result;
            }
        }

        WakeOnLANMode IWakeOnLANManager.Modes
        {
            get
            {
                var result = WakeOnLANMode.None;
                if (this["WakeOnMagicPacket"] == true)
                    result |= WakeOnLANMode.MagicPacket;
                if (this["WakeOnPattern"] == true)
                    result |= Pattern;

                return result;
            }

            set
            {
                this["WakeOnMagicPacket"] = value.HasFlag(WakeOnLANMode.MagicPacket);
                this["WakeOnPattern"] = (value & Pattern) != WakeOnLANMode.None;

                // TODO: Also configure ARP and NS offload? Does this matter?
            }
        }

        #region CIM access
        private CimInstance? FindPowerManagementInstance()
        {
            foreach (var instance in Session.EnumerateInstances(AdapterNamespace, "MSFT_NetAdapterPowerManagementSettingData"))
                if (string.Equals((string?)instance.CimInstanceProperties["Name"]?.Value, PhysicalAdapterName, StringComparison.OrdinalIgnoreCase))
                    return RefreshInstance(instance);

            return null;
        }

        private bool? this[string propertyName]
        {
            get
            {
                if (FindPowerManagementInstance()?.CimInstanceProperties[propertyName]?.Value is UInt32 value)
                {
                    switch (value)
                    {
                        case 1:
                            return false;
                        case 2:
                            return true;

                        default:
                            throw new ArgumentOutOfRangeException(propertyName);
                    }
                }

                return null;
            }

            set
            {
                if (FindPowerManagementInstance() is CimInstance instance)
                {
                    if (instance.CimInstanceProperties[propertyName] is CimProperty property)
                    {
                        uint target = (uint)(value ?? throw new ArgumentNullException(nameof(value)) ? 2 : 1);

                        if (!property.Value.Equals(target))
                        {
                            property.Value = target;

                            Session.ModifyInstance(AdapterNamespace, instance);

                            Logger.LogTrace("MSFT_NetAdapterPowerManagement.{Property} = {Value}", propertyName, value);
                        }
                    }
                    else
                        throw new InvalidOperationException($"{propertyName} not found");
                }
                else
                    throw new InvalidOperationException($"MSFT_NetAdapterPowerManagement not found");
            }
        }
        #endregion
    }
}
