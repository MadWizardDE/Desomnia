namespace MadWizard.Desomnia.Network.Manager
{
    internal class CompositeWakeOnLANManager(IEnumerable<IWakeOnLANManager> managers) : IWakeOnLANManager
    {
        public const WakeOnLANMode Pattern = WakeOnLANMode.None
            | WakeOnLANMode.Unicast
            | WakeOnLANMode.Broadcast
            | WakeOnLANMode.Multicast
            | WakeOnLANMode.ARP
            | WakeOnLANMode.Filter;

        WakeOnLANMode IWakeOnLANManager.SupportedModes
        {
            get => managers.Aggregate((WakeOnLANMode)~0, (supported, m) => supported & m.SupportedModes);
        }

        WakeOnLANMode IWakeOnLANManager.Modes
        {
            get => managers.Aggregate((WakeOnLANMode)~0, (modes, m) => modes & m.Modes);

            set
            {
                foreach (var manager in managers)
                    manager.Modes = value;
            }
        }
    }
}
