using Makaretu.Dns;
using PacketDotNet;
using System.Net;

namespace MadWizard.Desomnia.Network.Neighborhood.Services
{
    public class TransportNetworkService(string name, IPPort port) : NetworkService(name)
    {
        public virtual string ServiceName
        {
            get
            {
                field ??= Name.ToLower();

                return field;
            }

            set;
        }

        /// <summary>The DNS-SD service type of a service, e.g. "_ssh._tcp.local"; <c>null</c> if it has no service name set.</summary>
        public DomainName LocalDomainName => field ??= new DomainName($"_{ServiceName}", $"_{port.Protocol.ToString().ToLower()}", "local");

        public IPPort Port => port;

        protected internal virtual IEnumerable<IPPort> Ports
        {
            get
            {
                yield return port;
            }
        }

        public override bool Accepts(Packet packet)
        {
            if (packet.Extract<TransportPacket>() is TransportPacket transport)
                return Ports.Any(service => service.Accepts(transport));

            return false;
        }

        public bool Serves(IPPort another) => Ports.Any(service => service == another);

        public override string ToString()
        {
            return $"{port} (\"{Name}\")";
        }
    }

    public static class NetworkHostExt
    {
        extension (IEnumerable<NetworkService> services)
        {
            public TransportNetworkService? WithPort(IPPort port)
            {
                return services.OfType<TransportNetworkService>().Where(t => t.Serves(port)).FirstOrDefault();
            }
        }
    }
}
