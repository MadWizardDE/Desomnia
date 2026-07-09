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

        /// <summary>
        /// Optional DNS-SD instance label the service is advertised under when it differs from the
        /// host's name (see <see cref="Neighborhood.Services.TransportNetworkService.InstanceName"/>).
        /// </summary>
        public string?          InstanceName        { get; set; }

        public IPProtocol       Protocol            { get; set; } = IPProtocol.TCP;
        public ushort           Port                { get; set; }

        public IPPort IPPort => new(Protocol, Port);

        public virtual TransportNetworkService Service => new(Name, IPPort)
        {
            ServiceName = ServiceName!
        };
    }
}
