namespace MadWizard.Desomnia.Network.Neighborhood.Events
{
    public class ServiceEventArgs(NetworkService service) : EventArgs
    {
        public NetworkService Service => service;
    }
}
