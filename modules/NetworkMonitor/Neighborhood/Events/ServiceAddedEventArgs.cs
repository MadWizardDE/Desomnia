namespace MadWizard.Desomnia.Network.Neighborhood.Events
{
    public class ServiceAddedEventArgs(NetworkService service, DateTime? expires) : ServiceEventArgs(service)
    {
        public DateTime? Expires => expires;
    }
}
