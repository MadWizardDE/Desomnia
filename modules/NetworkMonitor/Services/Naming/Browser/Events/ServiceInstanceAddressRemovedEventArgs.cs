using System.Net;

namespace MadWizard.Desomnia.Network.Naming.Browser.Events
{
    public class ServiceInstanceAddressRemovedEventArgs(IPAddress ip, ServiceInstanceRemovedReason reason) : ServiceInstanceAddressEventArgs(ip)
    {
        public ServiceInstanceRemovedReason Reason => reason;

        public bool HasExpired => Reason == ServiceInstanceRemovedReason.Expired;
    }
}
