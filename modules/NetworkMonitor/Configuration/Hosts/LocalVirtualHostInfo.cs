using MadWizard.Desomnia.Configuration;

namespace MadWizard.Desomnia.Network.Configuration.Hosts
{
    public class LocalVirtualHostInfo : WatchedHostInfo
    {
        public LocalVirtualHostInfo()
        {
            OnMagicPacket = new ActionInfo("wake");
        }
    }
}
