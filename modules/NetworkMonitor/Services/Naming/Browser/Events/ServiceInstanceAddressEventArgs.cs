using System.Net;

namespace MadWizard.Desomnia.Network.Naming.Browser.Events
{
    public class ServiceInstanceAddressEventArgs(IPAddress address) : EventArgs
    {
        public IPAddress Address => address;
    }
}
