using MadWizard.Desomnia.Network.Neighborhood.Services;
using System.Net;

namespace MadWizard.Desomnia.Network.Configuration.Services
{
    /// <summary>
    /// https://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.xml
    /// </summary>
    public class ServiceInfo()
    {
        public required string  Name                { get; set; }
        public string?          ServiceName         { get; set; }

        public IPProtocol       Protocol            { get; set; } = IPProtocol.TCP;
        public ushort           Port                { get; set; }

        public IPPort IPPort => new(Protocol, Port);

        public virtual TransportNetworkService Service => new(Name, IPPort)
        {
            ServiceName = ServiceName!
        };
    }
}
