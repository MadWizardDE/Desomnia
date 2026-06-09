using MadWizard.Desomnia.Network.Neighborhood;
using PacketDotNet;
using System.Data;
using System.Net;

namespace MadWizard.Desomnia.Network.Demand
{
    public class DemandEvent(NetworkHost host, IPAddress? ip = null, IEnumerable<EthernetPacket>? packets = null) : Event("Demand"), IIEnumerable<EthernetPacket>
    {
        public EthernetPacket TriggerPacket => this.First();

        private IPPacket? TriggerIPPacket => this.Select(p => p.Extract<IPPacket>()).Where(i => i is not null && i.IsIPUnicast()).FirstOrDefault() is IPPacket ip ? ip : null;

        public NetworkHost Host { get; private init; } = host;
        public NetworkService? Service { get; set; }

        public IPAddress?   SourceAddress   => TriggerIPPacket?.SourceAddress;
        public IPEndPoint?  TargetEndPoint  => TriggerIPPacket?.DestinationEndPoint ?? (ip is not null ? new IPEndPoint(ip, 0) : null);

        public bool CanBeForwarded { get; set; } = false;

        public DemandEvent(DemandRequest request) : this(request.Host, request.TargetAddress, request)
        {

        }

        public IEnumerator<EthernetPacket> GetEnumerator()
        {
            return packets?.GetEnumerator() ?? Enumerable.Empty<EthernetPacket>().GetEnumerator();
        }
    }
}
