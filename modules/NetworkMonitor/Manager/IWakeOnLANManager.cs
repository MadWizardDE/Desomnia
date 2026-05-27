namespace MadWizard.Desomnia.Network.Manager
{
    public interface IWakeOnLANManager
    {
        public WakeOnLANMode SupportedModes { get; }
        public WakeOnLANMode Modes          { get; set; }
    }

    [Flags]
    public enum WakeOnLANMode
    {
        None        = 0,        

        PHY         = 1 << 0,
        Unicast     = 1 << 1,
        Multicast   = 1 << 2,
        Broadcast   = 1 << 3,
        ARP         = 1 << 4,
        MagicPacket = 1 << 5,
        SecureOn    = 1 << 6,
        Filter      = 1 << 7
    }
}
