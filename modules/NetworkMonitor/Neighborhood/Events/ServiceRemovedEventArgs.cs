namespace MadWizard.Desomnia.Network.Neighborhood.Events
{
    public class ServiceRemovedEventArgs(NetworkService service, bool expired = false) : ServiceEventArgs(service)
    {
        public bool HasExpired => expired;
    }
}
